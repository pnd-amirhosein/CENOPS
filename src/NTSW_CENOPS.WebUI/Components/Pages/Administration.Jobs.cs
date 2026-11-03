using NTSW_CENOPS.Components.Common.Dates;
using NTSW_CENOPS.Application.Jobs;
using NTSW_CENOPS.Components.DataTable.Core;
using NTSW_CENOPS.Components.DataTable.Models;
using NTSW_CENOPS.Domain.Authorization;
using Microsoft.AspNetCore.Components;

namespace NTSW_CENOPS.Components.Pages;

public partial class Administration
{
    private const string JobsTab = "jobs";
    private bool _canViewJobs;
    private bool _canManageJobs;
    private bool _canTestJobs;
    private readonly DataTableController<AutomatedJobModel> _jobsController = new();
    private List<AutomatedJobModel> _jobs = new();
    private List<AutomatedJobModel> _filteredJobs = new();
    private List<FilterDefinition> _jobFilters = new();
    private AutomatedJobModel? _sqlJob;
    private string _sqlDraft = "";
    private bool _editingSql;
    private bool _savingSql;
    private bool _showSqlSaveConfirmation;
    private AutomatedJobModel? _testingJob;
    private AutomatedJobModel? _historyJob;
    private IReadOnlyList<JobSourceOption> _jobSources = Array.Empty<JobSourceOption>();
    private string SourceLabel(string key) => _jobSources.FirstOrDefault(x => x.Key == key)?.DisplayName ?? key;
    private bool _testRunning;
    private List<AutomatedJobRunModel> _runHistory = new();
    private string? _jobModalError;

    private List<ColumnDefinition<AutomatedJobModel>> _jobColumns = new();

    protected override void OnInitialized()
    {
        _jobColumns = new()
        {
            new() { Title = "نام کار", PropertyName = nameof(AutomatedJobModel.Name), Value = x => x.Name, Type = ColumnType.Text, width = "23%" },
            new() { Title = "دقیقه", PropertyName = nameof(AutomatedJobModel.IterationMinutes), Value = x => x.IterationMinutes, Type = ColumnType.Number, width = "10%" },
            new() { Title = "منبع", PropertyName = nameof(AutomatedJobModel.SourceKey), Value = x => x.SourceKey, DisplayValue = x => SourceLabel(x.SourceKey), Type = ColumnType.Text, width = "12%" },
            new() { Title = "فعال", PropertyName = nameof(AutomatedJobModel.IsEnabled), Value = x => x.IsEnabled, Type = ColumnType.Checkbox, width = "8%" },
            new() { Title = "ثبت‌کننده", PropertyName = nameof(AutomatedJobModel.AuthorDisplayName), Value = x => x.AuthorDisplayName, IsReadOnly = true, Type = ColumnType.Text, width = "13%" },
            new() { Title = "آخرین اجرا", PropertyName = nameof(AutomatedJobModel.LastCalledAtUtc), Value = x => x.LastCalledAtUtc is null ? "—" : JalaliDate.DateTime(x.LastCalledAtUtc.Value.ToLocalTime()), IsReadOnly = true, Type = ColumnType.Text, width = "18%" },
            new() { Title = "نتیجه", PropertyName = nameof(AutomatedJobModel.LastResult), Value = x => x.LastResult, IsReadOnly = true, Type = ColumnType.Text, width = "16%" }
        };
    }

    private async Task ConfigureJobsPermissionsAsync()
    {
        var actor = (await AuthState.GetAuthenticationStateAsync()).User;
        _canViewJobs = await AccessService.HasAccessAsync(actor, AccessKeys.ViewJobs);
        _canManageJobs = _canViewJobs && await JobsService.CanManageAsync(actor);
        _canTestJobs = _canViewJobs && await JobsService.CanTestAsync(actor);
        if (!_canViewJobs) _tabs = _tabs.Where(x => x.Key != JobsTab).ToArray();
        if (_canViewJobs)
        {
            _jobSources = JobSources.GetSources();
            ConfigureJobsTable();
            await LoadJobsAsync();
        }
    }

    private void ConfigureJobsTable()
    {
        var sourceColumn = _jobColumns.Single(x => x.PropertyName == nameof(AutomatedJobModel.SourceKey));
        sourceColumn.Options = _jobSources.Select(x => x.Key).ToArray();
        sourceColumn.OptionLabels = _jobSources.ToDictionary(x => x.Key, x => x.DisplayName);
        _jobsController.OperationFailed = ShowTableError;
        _jobsController.CreateNewItem = () => new AutomatedJobModel { SourceKey = _jobSources.FirstOrDefault()?.Key ?? "", IterationMinutes = 60, IsEnabled = false };
        _jobsController.CloneForEdit = x => new AutomatedJobModel
        {
            Id = x.Id, Name = x.Name, IterationMinutes = x.IterationMinutes,
            SourceKey = x.SourceKey, IsEnabled = x.IsEnabled, SqlValue = x.SqlValue,
            UserId = x.UserId, AuthorDisplayName = x.AuthorDisplayName, LastResult = x.LastResult,
            LastCalledAtUtc = x.LastCalledAtUtc
        };
        _jobsController.SetPageSize(7);
        _jobsController.OnCreate = async model =>
        {
            var outcome = await JobsService.SaveAsync(model, (await AuthState.GetAuthenticationStateAsync()).User);
            if (!outcome.Succeeded) throw new NTSW_CENOPS.Components.Common.Notices.AppActionRejectedException(outcome.Message);
            Notices.Success(outcome.Message);
            await LoadJobsAsync();
        };
        _jobsController.OnUpdate = _jobsController.OnCreate;
        _jobsController.OnDelete = async model =>
        {
            var outcome = await JobsService.DeleteAsync(model.Id, (await AuthState.GetAuthenticationStateAsync()).User);
            if (!outcome.Succeeded) throw new NTSW_CENOPS.Components.Common.Notices.AppActionRejectedException(outcome.Message);
            Notices.Success(outcome.Message);
            await LoadJobsAsync();
        };
        _jobsController.OnRefresh = LoadJobsAsync;
    }

    private async Task LoadJobsAsync()
    {
        if (!_canViewJobs) return;
        using var loading = Busy.Begin("در حال دریافت کارهای خودکار...");
        try
        {
            _jobs = (await JobsService.ListAsync((await AuthState.GetAuthenticationStateAsync()).User)).ToList();
            ApplyJobFilters(false);
        }
        catch (Exception) { Notices.Error("بارگیری کارهای خودکار انجام نشد."); }
        finally { await InvokeAsync(StateHasChanged); }
    }

    private async Task OnJobFiltersChanged(List<FilterDefinition> filters)
    {
        _jobFilters = CloneFilters(filters);
        await LoadJobsAsync();
        ApplyJobFilters(true);
    }
    private void ApplyJobFilters(bool resetPage)
    {
        _filteredJobs = ApplyFilters(_jobs, _jobFilters);
        if (resetPage) _jobsController.SetPage(1);
        _jobsController.SetTotal(_filteredJobs.Count);
    }

    private void OpenJobSql(AutomatedJobModel job)
    {
        _jobsController.NotifyInitialState();
        _sqlJob = job;
        _sqlDraft = job.SqlValue;
        _editingSql = false;
        _showSqlSaveConfirmation = false;
        _jobModalError = null;
    }
    private void CloseJobSql()
    {
        if (_savingSql) return;
        _sqlJob = null;
        _editingSql = false;
        _showSqlSaveConfirmation = false;
    }
    private async Task SaveJobSqlAsync()
    {
        if (_sqlJob is null || !_canManageJobs || _savingSql) return;
        _savingSql = true;
        using var loading = Busy.Begin("در حال ذخیره دستور SQL...");
        try
        {
            var outcome = await JobsService.UpdateSqlAsync(_sqlJob.Id, _sqlDraft, (await AuthState.GetAuthenticationStateAsync()).User);
            if (!outcome.Succeeded) { _jobModalError = outcome.Message; Notices.Error(outcome.Message); return; }
            Notices.Success(outcome.Message);
            _sqlJob = null;
            _editingSql = false;
            await LoadJobsAsync();
        }
        catch (Exception) { Notices.Error("دستور SQL ذخیره نشد."); }
        finally { _savingSql = false; _showSqlSaveConfirmation = false; }
    }

    private async Task OpenJobTestAsync(AutomatedJobModel job)
    {
        if (!_canTestJobs) return;
        if (string.IsNullOrWhiteSpace(job.SqlValue)) { Notices.Error("ابتدا SQL این کار را ثبت کنید."); return; }
        _jobsController.NotifyInitialState();
        _testingJob = job;
        _jobModalError = null;
    }
    private async Task OpenJobHistoryAsync(AutomatedJobModel job)
    {
        _jobsController.NotifyInitialState();
        _historyJob = job;
        _runHistory.Clear();
        await LoadJobRunsAsync(job.Id);
    }
    private async Task LoadJobRunsAsync(long id)
    {
        try { _runHistory = (await JobsService.RecentRunsAsync(id, (await AuthState.GetAuthenticationStateAsync()).User)).ToList(); }
        catch (Exception) { Notices.Error("تاریخچه اجرای کار دریافت نشد."); }
    }
    private async Task ExecuteJobTestAsync()
    {
        if (_testingJob is null || !_canTestJobs || _testRunning) return;
        _testRunning = true;
        using var loading = Busy.Begin("در حال اجرای آزمایشی کار...");
        try
        {
            var outcome = await JobsService.TestAsync(_testingJob.Id, (await AuthState.GetAuthenticationStateAsync()).User);
            if (outcome.Succeeded) Notices.Success(outcome.Message);
            else { _jobModalError = outcome.Message; Notices.Error(outcome.Message); }
            if (_historyJob?.Id == _testingJob.Id) await LoadJobRunsAsync(_testingJob.Id);
            await LoadJobsAsync();
        }
        catch (Exception) { _jobModalError = "اجرای آزمایشی ناموفق بود."; Notices.Error(_jobModalError); }
        finally { _testRunning = false; }
    }
}
