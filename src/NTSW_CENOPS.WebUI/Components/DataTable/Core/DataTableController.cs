using NTSW_CENOPS.Components.DataTable.Models;

namespace NTSW_CENOPS.Components.DataTable.Core;

public class DataTableController<T>
{
    public Func<T, Task>? OnCreate;
    public Func<T, Task>? OnUpdate;
    public Func<T, Task>? OnDelete;
    public Action<Exception>? OperationFailed;

    public Func<T, Task<bool>>? ValidateCreate;
    public Func<T, Task<bool>>? ValidateUpdate;

    public Func<T>? CreateNewItem { get; set; }
    public Func<T, T>? CloneForEdit { get; set; }

    public event Action? StateChanged;
    public Func<Task>? OnRefresh;
    public int Total { get; set; } = 10;
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;

    public int PageCount => Math.Max(1, (int)Math.Ceiling(Total / (double)PageSize));

    public event Action? PaginationChanged;
    public TableMode Mode { get; private set; } = TableMode.View;

    public T? SelectedItem { get; private set; }

    public T? EditingItem { get; private set; }

    public SortState Sort { get; } = new();

    public bool IsLoading { get; private set; }

    private void NotifyStateChanged()
    {
        StateChanged?.Invoke();
    }

    public void NotifyInitialState()
    {
        StateChanged?.Invoke();
    }

    public void SetPage(int page)
    {
        var targetPage = Math.Clamp(page, 1, PageCount);
        if (targetPage == Page)
            return;

        Page = targetPage;
        PaginationChanged?.Invoke();
        NotifyStateChanged();
    }

    public void SetPageSize(int size)
    {
        if (size <= 0) return;

        PageSize = size;
        Page = 1; // reset
        PaginationChanged?.Invoke();
        NotifyStateChanged();
    }
    
    public void SetTotal(int total)
    {
        if (total < 0) return;

        Total = total;
        // A refreshed or filtered result set must not leave a hidden row selected.
        if (Mode == TableMode.View)
            SelectedItem = default;

        if (Page > PageCount)
            Page = PageCount;

        PaginationChanged?.Invoke();
        NotifyStateChanged();
    }

    public void Select(T item)
    {
        if (Equals(SelectedItem, item))
            SelectedItem = default;
        else
            SelectedItem = item;

        NotifyStateChanged();
    }

    public bool CanEdit =>
        SelectedItem is not null &&
        Mode == TableMode.View;

    public bool CanDelete =>
        SelectedItem is not null &&
        Mode == TableMode.View;

    public bool IsEditing =>
        Mode == TableMode.Edit;

    public bool IsCreating =>
        Mode == TableMode.Create;

    public void BeginCreate()
    {
        if (CreateNewItem is null)
            throw new InvalidOperationException("CreateNewItem factory is not set.");

        EditingItem = CreateNewItem();
        Mode = TableMode.Create;

        NotifyStateChanged();
    }
    
    public async Task RefreshData()
    {
        if (IsLoading) return;
        IsLoading = true;
        NotifyStateChanged();
        try
        {
            if (OnRefresh is not null) await OnRefresh();
        }
        catch (Exception error)
        {
            OperationFailed?.Invoke(error);
        }
        finally
        {
            IsLoading = false;
            NotifyStateChanged();
        }
    }

    public void BeginEdit()
    {
        if (SelectedItem is null)
            return;

        EditingItem = CloneForEdit is null ? SelectedItem : CloneForEdit(SelectedItem);

        Mode = TableMode.Edit;

        NotifyStateChanged();
    }

    public void Cancel()
    {
        EditingItem = default;

        Mode = TableMode.View;

        NotifyStateChanged();
    }

    public async Task SaveAsync()
    {
        if (EditingItem is null || IsLoading) return;
        IsLoading = true;
        NotifyStateChanged();
        try
        {
            var isValid = Mode switch
            {
                TableMode.Create => ValidateCreate is null || await ValidateCreate(EditingItem),
                TableMode.Edit => ValidateUpdate is null || await ValidateUpdate(EditingItem),
                _ => true
            };
            if (!isValid) return;

            switch (Mode)
            {
                case TableMode.Create:
                    if (OnCreate is not null) await OnCreate(EditingItem);
                    break;
                case TableMode.Edit:
                    if (OnUpdate is not null) await OnUpdate(EditingItem);
                    break;
                default: return;
            }
            EditingItem = default;
            SelectedItem = default;
            Mode = TableMode.View;
        }
        catch (Exception error)
        {
            // Rejected save: preserve the draft instead of falsely completing the edit.
            OperationFailed?.Invoke(error);
        }
        finally
        {
            IsLoading = false;
            NotifyStateChanged();
        }
    }

    public async Task DeleteAsync()
    {
        if (SelectedItem is null || IsLoading) return;
        IsLoading = true;
        NotifyStateChanged();
        try
        {
            if (OnDelete is not null) await OnDelete(SelectedItem);
            SelectedItem = default;
            EditingItem = default;
            Mode = TableMode.View;
        }
        catch (Exception error)
        {
            OperationFailed?.Invoke(error);
        }
        finally
        {
            IsLoading = false;
            NotifyStateChanged();
        }
    }
}
