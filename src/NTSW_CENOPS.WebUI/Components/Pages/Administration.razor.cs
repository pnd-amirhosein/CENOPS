using NTSW_CENOPS.Application.Access;
using NTSW_CENOPS.Domain.Authorization;
using NTSW_CENOPS.Application.Administration;
using NTSW_CENOPS.Components.Common.Tabs;
using NTSW_CENOPS.Components.Common.Selection;
using NTSW_CENOPS.Components.Common.Notices;
using NTSW_CENOPS.Components.Common.Loading;
using NTSW_CENOPS.Components.DataTable.Core;
using NTSW_CENOPS.Components.DataTable.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;

namespace NTSW_CENOPS.Components.Pages;

public partial class Administration : ComponentBase
{
    private const string RolesTab = "roles";
    private const string UsersTab = "users";
    private const string AccessTab = "access";
    private const string RolesToolbarKey = "roles-toolbar";
    private const string RolesTableKey = "roles-table";
    private const string UsersToolbarKey = "users-toolbar";
    private const string UsersTableKey = "users-table";
    private const string AccessToolbarKey = "access-toolbar";
    private const string AccessTableKey = "access-table";

    [Inject]
    private IIdentityAdministrationService AdministrationService { get; set; } = default!;

    [Inject] private AppNoticeService Notices { get; set; } = default!;
    [Inject] private AppBusyService Busy { get; set; } = default!;
    [Inject] private AuthenticationStateProvider AuthState { get; set; } = default!;
    [Inject] private IAccessService AccessService { get; set; } = default!;
    [Inject] private NTSW_CENOPS.Application.Jobs.IAutomatedJobService JobsService { get; set; } = default!;
    [Inject] private NTSW_CENOPS.Application.Jobs.IJobSourceCatalog JobSources { get; set; } = default!;

    private readonly DataTableController<AdminUserModel> _usersController = new();
    private readonly DataTableController<AdminRoleModel> _rolesController = new();
    private readonly DataTableController<AccessModel> _accessController = new();

    private List<AdminUserModel> _users = new();
    private List<AdminRoleModel> _roles = new();
    private List<AccessModel> _accesses = new();
    private List<AccessModel> _filteredAccesses = new();
    private List<AdminUserModel> _filteredUsers = new();
    private List<AdminRoleModel> _filteredRoles = new();

    private List<FilterDefinition> _userFilters = new();
    private List<FilterDefinition> _roleFilters = new();
    private List<FilterDefinition> _accessFilters = new();
    private bool _canManageAccess;
    private AdminRoleModel? _accessEditorRole;
    private HashSet<int> _selectedAccessIds = new();
    private string _accessSearch = string.Empty;
    private string? _accessEditorError;
    private bool _savingAccesses;

    // Owner-only: access -> active roles (reverse editor).
    private AccessModel? _roleEditorAccess;
    private HashSet<int> _accessRoleIds = new();
    private string _accessRolesSearch = string.Empty;
    private string? _accessRolesError;
    private bool _savingAccessRoles;
    private bool _showAccessGuide;

    // Role -> active users (reverse of the user -> roles badge editor).
    private AdminRoleModel? _userEditorRole;
    private HashSet<int> _selectedUserIds = new();
    private string _availableUserSearch = string.Empty;
    private string _assignedUserSearch = string.Empty;
    private string? _roleUsersError;
    private bool _savingRoleUsers;

    private IReadOnlySet<int> OwnerRequiredAccessIds => _accessEditorRole?.Name.Equals("Owner", StringComparison.OrdinalIgnoreCase) == true
        ? _accesses.Where(x => x.Key == AccessKeys.Settings || x.Key == AccessKeys.ManageAccess).Select(x => x.Id).ToHashSet()
        : new HashSet<int>();
    private IReadOnlySet<int> RecoveryProtectedRoleIds => _roleEditorAccess?.Key is AccessKeys.Settings or AccessKeys.ManageAccess
        ? _roles.Where(x => x.Name.Equals("Owner", StringComparison.OrdinalIgnoreCase)).Select(x => x.Id).ToHashSet()
        : new HashSet<int>();

    private IReadOnlyList<CheckboxListOption> AccessCheckboxOptions => SearchedAccesses
        .Select(x => new CheckboxListOption(x.Id, x.Name, x.Key, x.Description)).ToList();
    private IReadOnlyList<CheckboxListOption> RoleCheckboxOptions => _roles
        .Where(x => string.IsNullOrWhiteSpace(_accessRolesSearch) || x.Name.Contains(_accessRolesSearch, StringComparison.OrdinalIgnoreCase))
        .Select(x => new CheckboxListOption(x.Id, x.Name)).ToList();

    private IEnumerable<AdminUserModel> AvailableUsers => _users
        .Where(x => !_selectedUserIds.Contains(x.Id))
        .Where(x => MatchesUserSearch(x, _availableUserSearch));
    private IEnumerable<AdminUserModel> AssignedUsers => _users
        .Where(x => _selectedUserIds.Contains(x.Id))
        .Where(x => MatchesUserSearch(x, _assignedUserSearch));
    private static bool MatchesUserSearch(AdminUserModel user, string query) =>
        string.IsNullOrWhiteSpace(query) ||
        user.Username.Contains(query, StringComparison.OrdinalIgnoreCase) ||
        user.DisplayName.Contains(query, StringComparison.OrdinalIgnoreCase);

    private async Task OpenAccessRolesEditorAsync(AccessModel access)
    {
        if (!_canManageAccess) return;
        _accessController.NotifyInitialState();
        _accessRolesError = null;
        _accessRolesSearch = string.Empty;
        using var loading = Busy.Begin("در حال دریافت نقش‌های دسترسی...");
        try
        {
            var actor = (await AuthState.GetAuthenticationStateAsync()).User;
            await LoadRolesAsync();
            _accessRoleIds = (await AccessService.GetAccessRoleIdsAsync(access.Id, actor)).ToHashSet();
            _roleEditorAccess = access;
        }
        catch (Exception) { Notices.Error("دریافت نقش‌های دسترسی انجام نشد."); }
    }

    private void UpdateAccessRoleSelection(CheckboxListChange change)
    {
        if (change.IsChecked) _accessRoleIds.Add(change.Id);
        else _accessRoleIds.Remove(change.Id);
    }

    private void CloseAccessRolesEditor() { if (!_savingAccessRoles) _roleEditorAccess = null; }

    private async Task SaveAccessRolesAsync()
    {
        if (_roleEditorAccess is null || _savingAccessRoles || !_canManageAccess) return;
        _savingAccessRoles = true;
        _accessRolesError = null;
        using var loading = Busy.Begin("در حال ذخیره نقش‌های دسترسی...");
        try
        {
            var actor = (await AuthState.GetAuthenticationStateAsync()).User;
            var result = await AccessService.UpdateAccessRolesAsync(_roleEditorAccess.Id, _accessRoleIds.ToArray(), actor);
            if (!result.Succeeded)
            {
                _accessRolesError = result.ErrorMessage ?? "ذخیره نقش‌ها انجام نشد.";
                Notices.Error(_accessRolesError);
                return;
            }
            _roleEditorAccess = null;
            await LoadRolesAsync();
            Notices.Success("نقش‌های متصل به دسترسی ذخیره شدند.");
        }
        catch (Exception)
        {
            _accessRolesError = "خطا در ذخیره نقش‌های دسترسی.";
            Notices.Error(_accessRolesError);
        }
        finally { _savingAccessRoles = false; }
    }

    private async Task OpenRoleUsersEditorAsync(AdminRoleModel role)
    {
        _rolesController.NotifyInitialState();
        _roleUsersError = null;
        _availableUserSearch = _assignedUserSearch = string.Empty;
        using var loading = Busy.Begin("در حال دریافت کاربران نقش...");
        try
        {
            var actor = (await AuthState.GetAuthenticationStateAsync()).User;
            await LoadUsersAsync();
            _selectedUserIds = (await AdministrationService.GetRoleUserIdsAsync(role.Id, actor)).ToHashSet();
            _userEditorRole = role;
        }
        catch (UnauthorizedAccessException) { Notices.Error("فقط مالک سامانه می‌تواند کاربران نقش‌های سیستمی را ویرایش کند."); }
        catch (Exception) { Notices.Error("دریافت کاربران نقش انجام نشد."); }
    }
    private void AddUserToRole(int id) => _selectedUserIds.Add(id);
    private void RemoveUserFromRole(int id) => _selectedUserIds.Remove(id);
    private void CloseRoleUsersEditor() { if (!_savingRoleUsers) _userEditorRole = null; }

    private async Task SaveRoleUsersAsync()
    {
        if (_userEditorRole is null || _savingRoleUsers) return;
        _savingRoleUsers = true;
        _roleUsersError = null;
        using var loading = Busy.Begin("در حال ذخیره کاربران نقش...");
        try
        {
            var actor = (await AuthState.GetAuthenticationStateAsync()).User;
            var result = await AdministrationService.UpdateRoleUsersAsync(_userEditorRole.Id, _selectedUserIds.ToArray(), actor);
            if (!result.Succeeded)
            {
                _roleUsersError = result.ErrorMessage ?? "ذخیره کاربران انجام نشد.";
                Notices.Error(_roleUsersError);
                return;
            }
            _userEditorRole = null;
            await LoadUsersAsync();
            Notices.Success("کاربران نقش با موفقیت ذخیره شدند.");
        }
        catch (Exception)
        {
            _roleUsersError = "خطا در ذخیره کاربران نقش.";
            Notices.Error(_roleUsersError);
        }
        finally { _savingRoleUsers = false; }
    }

    private string _activeTab = RolesTab;
    private bool _isFetching;
    private string? _statusMessage;
    private AdminUserModel? _roleEditorUser;
    private HashSet<int> _selectedRoleIds = new();
    private string _availableRoleSearch = string.Empty;
    private string _assignedRoleSearch = string.Empty;
    private bool _savingRoles;
    private bool _isOwner;
    private bool CanChangeRole(AdminRoleModel role) => _isOwner ||
        (!string.Equals(role.Name, "Owner", StringComparison.OrdinalIgnoreCase) &&
         !string.Equals(role.Name, "Admin", StringComparison.OrdinalIgnoreCase));
    private string? _roleEditorError;

    private IEnumerable<AdminRoleModel> AvailableRoles => _roles
        .Where(role => !_selectedRoleIds.Contains(role.Id))
        .Where(role => role.Name.Contains(_availableRoleSearch, StringComparison.OrdinalIgnoreCase));
    private IEnumerable<AdminRoleModel> AssignedRoles => _roles
        .Where(role => _selectedRoleIds.Contains(role.Id))
        .Where(role => role.Name.Contains(_assignedRoleSearch, StringComparison.OrdinalIgnoreCase));

    private void OpenRoleEditor(AdminUserModel user)
    {
        _usersController.NotifyInitialState(); // close the DataTable row menu before opening a modal
        _roleEditorUser = user;
        _selectedRoleIds = _roles
            .Where(role => user.Roles.Contains(role.Name, StringComparer.OrdinalIgnoreCase))
            .Select(role => role.Id).ToHashSet();
        _availableRoleSearch = _assignedRoleSearch = string.Empty;
        _roleEditorError = null;
    }
    private void CloseRoleEditor() { if (!_savingRoles) _roleEditorUser = null; }
    private void AssignRole(int id) => _selectedRoleIds.Add(id);
    private void UnassignRole(int id) => _selectedRoleIds.Remove(id);
    private async Task SaveRoleAssignmentsAsync()
    {
        if (_roleEditorUser is null || _savingRoles) return;
        _savingRoles = true;
        _roleEditorError = null;
        using var loading = Busy.Begin("در حال ذخیره نقش‌های کاربر...");
        try
        {
            var result = await AdministrationService.UpdateUserRolesAsync(_roleEditorUser.Id, _selectedRoleIds.ToArray(), (await AuthState.GetAuthenticationStateAsync()).User);
            if (!result.Succeeded)
            {
                _roleEditorError = result.ErrorMessage ?? "ذخیره نقش‌ها انجام نشد.";
                Notices.Error(_roleEditorError);
                return;
            }
            _roleEditorUser = null;
            Notices.Success("نقش‌های کاربر با موفقیت ذخیره شد.");
            await LoadUsersAsync();
        }
        catch (Exception)
        {
            _roleEditorError = "خطا در ذخیره نقش‌های کاربر. لطفاً دوباره تلاش کنید.";
            Notices.Error(_roleEditorError);
        }
        finally { _savingRoles = false; }
    }

    private IReadOnlyList<AppTabItem> _tabs = new[]
    {
        new AppTabItem { Key = RolesTab, Title = "مدیریت نقش‌ها", IconSvg = "<svg viewBox='0 0 24 24' fill='none' stroke='currentColor' stroke-width='1.65' stroke-linecap='round' stroke-linejoin='round'><path d='M12 3 20 7v5c0 5-3.4 7.3-8 9-4.6-1.7-8-4-8-9V7l8-4Z'/><path d='m9.2 12 2 2 4-4'/></svg>" },
        new AppTabItem { Key = UsersTab, Title = "مدیریت کاربران", IconSvg = "<svg viewBox='0 0 24 24' fill='none' stroke='currentColor' stroke-width='1.65' stroke-linecap='round' stroke-linejoin='round'><circle cx='12' cy='8' r='3.25'/><path d='M5 20v-1c0-3.4 3.1-5.4 7-5.4s7 2 7 5.4v1'/></svg>" },
        new AppTabItem { Key = JobsTab, Title = "کارهای خودکار", IconSvg = "<svg viewBox='0 0 24 24' fill='none' stroke='currentColor' stroke-width='1.7' stroke-linecap='round'><circle cx='12' cy='12' r='9'/><path d='M12 7v5l3.5 2M4 4l2 2'/></svg>" },
        new AppTabItem { Key = AccessTab, Title = "مدیریت دسترسی‌ها", IconSvg = "<svg viewBox='0 0 24 24' fill='none' stroke='currentColor' stroke-width='1.65' stroke-linecap='round' stroke-linejoin='round'><rect x='5' y='10' width='14' height='11' rx='2'/><path d='M8 10V7a4 4 0 0 1 8 0v3m-4 5v2'/></svg>" }
    };

    private readonly List<ColumnDefinition<AdminUserModel>> _userColumns = new()
    {
        new ColumnDefinition<AdminUserModel>
        {
            Title = "نام کاربری",
            PropertyName = nameof(AdminUserModel.Username),
            Value = x => x.Username,
            width = "28%",
            Type = ColumnType.Text
        },
        new ColumnDefinition<AdminUserModel>
        {
            Title = "نام نمایشی",
            PropertyName = nameof(AdminUserModel.DisplayName),
            Value = x => x.DisplayName,
            width = "32%",
            Type = ColumnType.Text
        },
        new ColumnDefinition<AdminUserModel>
        {
            Title = "نقش‌ها",
            PropertyName = nameof(AdminUserModel.Roles),
            Value = x => x.Roles,
            width = "40%",
            Type = ColumnType.List
        }
    };

    private readonly List<ColumnDefinition<AdminRoleModel>> _roleColumns = new()
    {
        new ColumnDefinition<AdminRoleModel>
        {
            Title = "نام نقش",
            PropertyName = nameof(AdminRoleModel.Name),
            Value = x => x.Name,
            width = "28%",
            Type = ColumnType.Text
        },
        new ColumnDefinition<AdminRoleModel>
        {
            Title = "دسترسی‌ها",
            PropertyName = nameof(AdminRoleModel.Accesses),
            Value = x => x.Accesses,
            width = "72%",
            Type = ColumnType.List
        }
    };

    private readonly List<ColumnDefinition<AccessModel>> _accessColumns = new()
    {
        new() { Title = "کد دسترسی", PropertyName = nameof(AccessModel.Key), Value = x => x.Key, Type = ColumnType.Text, width = "30%" },
        new() { Title = "عنوان دسترسی", PropertyName = nameof(AccessModel.Name), Value = x => x.Name, Type = ColumnType.Text, width = "30%" },
        new() { Title = "توضیحات", PropertyName = nameof(AccessModel.Description), Value = x => x.Description, Type = ColumnType.Text, width = "40%" }
    };

    private IEnumerable<AccessModel> SearchedAccesses => _accesses.Where(a =>
        string.IsNullOrWhiteSpace(_accessSearch) || a.Name.Contains(_accessSearch, StringComparison.OrdinalIgnoreCase)
        || a.Key.Contains(_accessSearch, StringComparison.OrdinalIgnoreCase));

    private async Task OpenAccessEditorAsync(AdminRoleModel role)
    {
        if (!_canManageAccess) return;
        _rolesController.NotifyInitialState();
        _accessEditorError = null;
        _accessSearch = string.Empty;
        using var loading = Busy.Begin("در حال دریافت دسترسی‌های نقش...");
        try
        {
            var actor = (await AuthState.GetAuthenticationStateAsync()).User;
            _selectedAccessIds = (await AccessService.GetRoleAccessIdsAsync(role.Id, actor)).ToHashSet();
            _accessEditorRole = role;
        }
        catch (Exception) { Notices.Error("دریافت دسترسی‌های نقش انجام نشد."); }
    }

    private void ToggleRoleAccess(int id, bool enabled)
    {
        if (enabled) _selectedAccessIds.Add(id);
        else _selectedAccessIds.Remove(id);
    }

    private void CloseAccessEditor() { if (!_savingAccesses) _accessEditorRole = null; }

    private async Task SaveRoleAccessesAsync()
    {
        if (_accessEditorRole is null || _savingAccesses || !_canManageAccess) return;
        _savingAccesses = true;
        _accessEditorError = null;
        using var loading = Busy.Begin("در حال ذخیره مجوزهای نقش...");
        try
        {
            var actor = (await AuthState.GetAuthenticationStateAsync()).User;
            var result = await AccessService.UpdateRoleAccessesAsync(_accessEditorRole.Id, _selectedAccessIds.ToArray(), actor);
            if (!result.Succeeded)
            {
                _accessEditorError = result.ErrorMessage ?? "ذخیره دسترسی‌ها انجام نشد.";
                Notices.Error(_accessEditorError);
                return;
            }
            _accessEditorRole = null;
            Notices.Success("دسترسی‌های نقش ذخیره شد.");
            await LoadRolesAsync();
        }
        catch (Exception) { _accessEditorError = "خطا در ذخیره دسترسی‌های نقش."; Notices.Error(_accessEditorError); }
        finally { _savingAccesses = false; }
    }

    private void OnRoleAccessSelection(CheckboxListChange change) =>
        ToggleRoleAccess(change.Id, change.IsChecked);

    protected override async Task OnInitializedAsync()
    {
        var auth = await AuthState.GetAuthenticationStateAsync();
        _isOwner = auth.User.IsInRole("Owner");
        _canManageAccess = _isOwner && await AccessService.HasAccessAsync(auth.User, AccessKeys.ManageAccess);
        if (!_canManageAccess) _tabs = _tabs.Where(x => x.Key != AccessTab).ToArray();
        if (_canManageAccess) ConfigureAccessTable();
        ConfigureUsersTable();
        ConfigureRolesTable();

        await LoadRolesAsync();
        await LoadUsersAsync();
        if (_canManageAccess) await LoadAccessesAsync();
        await ConfigureJobsPermissionsAsync();
    }

    private void ConfigureUsersTable()
    {
        _usersController.OperationFailed = ShowTableError;
        _usersController.CreateNewItem = () => new AdminUserModel();
        _usersController.CloneForEdit = x => new AdminUserModel { Id = x.Id, Username = x.Username, DisplayName = x.DisplayName, Roles = x.Roles.ToList() };
        _usersController.SetPageSize(5);

        _usersController.OnCreate = async item =>
        {
            var result = await AdministrationService.CreateUserAsync(item);
            SetOperationResult(result, "کاربر جدید ایجاد شد.");
            await LoadUsersAsync();
        };

        _usersController.OnUpdate = async item =>
        {
            var result = await AdministrationService.UpdateUserAsync(item);
            SetOperationResult(result, "اطلاعات کاربر به‌روزرسانی شد.");
            await LoadUsersAsync();
        };

        _usersController.OnDelete = async item =>
        {
            var result = await AdministrationService.DeleteUserAsync(item.Id);
            SetOperationResult(result, "کاربر حذف شد.");
            await LoadUsersAsync();
        };

        _usersController.OnRefresh = LoadUsersAsync;
    }

    private void ConfigureRolesTable()
    {
        _rolesController.OperationFailed = ShowTableError;
        _rolesController.CreateNewItem = () => new AdminRoleModel();
        _rolesController.CloneForEdit = x => new AdminRoleModel { Id = x.Id, Name = x.Name, Accesses = x.Accesses.ToList() };
        _rolesController.SetPageSize(5);

        _rolesController.OnCreate = async item =>
        {
            var result = await AdministrationService.CreateRoleAsync(item);
            SetOperationResult(result, "نقش جدید ایجاد شد.");
            await LoadRolesAsync();
        };

        _rolesController.OnUpdate = async item =>
        {
            var result = await AdministrationService.UpdateRoleAsync(item);
            SetOperationResult(result, "نقش به‌روزرسانی شد.");
            await LoadRolesAsync();
            await LoadUsersAsync(); // role rename must refresh names in the user badge column
        };

        _rolesController.OnDelete = async item =>
        {
            var result = await AdministrationService.DeleteRoleAsync(item.Id);
            SetOperationResult(result, "نقش حذف شد.");
            await LoadRolesAsync();
        };

        _rolesController.OnRefresh = LoadRolesAsync;
    }

    private void ConfigureAccessTable()
    {
        _accessController.OperationFailed = ShowTableError;
        _accessController.CreateNewItem = () => new AccessModel();
        _accessController.CloneForEdit = a => new AccessModel { Id = a.Id, Key = a.Key, Name = a.Name, Description = a.Description };
        _accessController.SetPageSize(5);
        _accessController.OnCreate = async item =>
        {
            SetOperationResult(await AccessService.CreateAsync(item, (await AuthState.GetAuthenticationStateAsync()).User), "دسترسی ایجاد شد.");
            await LoadAccessesAsync();
        };
        _accessController.OnUpdate = async item =>
        {
            SetOperationResult(await AccessService.UpdateAsync(item, (await AuthState.GetAuthenticationStateAsync()).User), "دسترسی به‌روزرسانی شد.");
            await LoadAccessesAsync();
            await LoadRolesAsync();
        };
        _accessController.OnDelete = async item =>
        {
            SetOperationResult(await AccessService.DeleteAsync(item.Id, (await AuthState.GetAuthenticationStateAsync()).User), "دسترسی حذف شد.");
            await LoadAccessesAsync();
        };
        _accessController.OnRefresh = LoadAccessesAsync;
    }

    private async Task LoadAccessesAsync()
    {
        if (!_canManageAccess) return;
        using var loading = Busy.Begin("در حال دریافت دسترسی‌ها...");
        try
        {
            _accesses = (await AccessService.GetAllAsync((await AuthState.GetAuthenticationStateAsync()).User)).ToList();
            ApplyAccessFilters(false);
        }
        catch (Exception) { Notices.Error("دریافت دسترسی‌ها انجام نشد."); }
        finally { await InvokeAsync(StateHasChanged); }
    }

    private async Task OnAccessFiltersChanged(List<FilterDefinition> filters)
    {
        _accessFilters = CloneFilters(filters);
        await LoadAccessesAsync();
        ApplyAccessFilters(true);
    }

    private void ApplyAccessFilters(bool resetPage)
    {
        _filteredAccesses = ApplyFilters(_accesses, _accessFilters);
        if (resetPage) _accessController.SetPage(1);
        _accessController.SetTotal(_filteredAccesses.Count);
    }

    private async Task LoadUsersAsync()
    {
        _isFetching = true;
        using var loading = Busy.Begin("در حال دریافت کاربران...");
        await InvokeAsync(StateHasChanged);
        try
        {
            _users = (await AdministrationService.GetUsersAsync()).ToList();
            ApplyUserFilters(resetPage: false);
        }
        catch (Exception)
        {
            Notices.Error("دریافت اطلاعات کاربران انجام نشد. لطفاً دوباره تلاش کنید.");
        }
        finally
        {
            _isFetching = false;
            await InvokeAsync(StateHasChanged);
        }
    }

    private async Task LoadRolesAsync()
    {
        _isFetching = true;
        using var loading = Busy.Begin("در حال دریافت نقش‌ها...");
        await InvokeAsync(StateHasChanged);
        try
        {
            _roles = (await AdministrationService.GetRolesAsync()).ToList();
            ApplyRoleFilters(resetPage: false);
        }
        catch (Exception)
        {
            Notices.Error("دریافت اطلاعات نقش‌ها انجام نشد. لطفاً دوباره تلاش کنید.");
        }
        finally
        {
            _isFetching = false;
            await InvokeAsync(StateHasChanged);
        }
    }

    private async Task OnUserFiltersChanged(List<FilterDefinition> filters)
    {
        _userFilters = CloneFilters(filters);
        // Get a fresh snapshot from Identity on Apply/Clear, then filter it.
        await LoadUsersAsync();
        ApplyUserFilters(resetPage: true);
    }

    private async Task OnRoleFiltersChanged(List<FilterDefinition> filters)
    {
        _roleFilters = CloneFilters(filters);
        await LoadRolesAsync();
        ApplyRoleFilters(resetPage: true);
    }

    private void ApplyUserFilters(bool resetPage)
    {
        _filteredUsers = ApplyFilters(_users, _userFilters);

        if (resetPage)
            _usersController.SetPage(1);

        _usersController.SetTotal(_filteredUsers.Count);
    }

    private void ApplyRoleFilters(bool resetPage)
    {
        _filteredRoles = ApplyFilters(_roles, _roleFilters);

        if (resetPage)
            _rolesController.SetPage(1);

        _rolesController.SetTotal(_filteredRoles.Count);
    }

    // Blazor's @bind-ActiveKey updates _activeTab on every tab selection.

    private void SetOperationResult(AdminOperationResult result, string successMessage)
    {
        if (!result.Succeeded)
            throw new AppActionRejectedException(result.ErrorMessage ?? "عملیات انجام نشد.");
        Notices.Success(successMessage);
    }

    private void ShowTableError(Exception error) => Notices.Error(
        error is AppActionRejectedException ? error.Message : "خطا در ارتباط با سرور. عملیات ذخیره نشد؛ دوباره تلاش کنید.");

    private static List<T> ApplyFilters<T>(IEnumerable<T> source, IReadOnlyList<FilterDefinition> filters)
    {
        if (filters.Count == 0)
            return source.ToList();

        return source
            .Where(item => filters.All(filter => DataTableFilterMatcher.Matches(item, filter)))
            .ToList();
    }

    private static List<FilterDefinition> CloneFilters(IEnumerable<FilterDefinition> filters)
    {
        return filters
            .Select(x => new FilterDefinition
            {
                PropertyName = x.PropertyName,
                Value = x.Value
            })
            .ToList();
    }
}
