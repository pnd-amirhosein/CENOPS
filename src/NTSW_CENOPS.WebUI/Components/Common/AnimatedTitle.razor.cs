using Microsoft.AspNetCore.Components;

namespace NTSW_CENOPS.Components.Common;

public partial class AnimatedTitle : ComponentBase, IDisposable
{
    [Parameter] public bool PreserveSpace { get; set; } = false;
    
    private readonly string[] _titles =
    [
        "عملیات های مرکزی",
        "Central Operations",
        "CenOps",
        "سیناپس",
    ];

    private int _index;
    private string _currentText = "عملیات های مرکزی";

    private PeriodicTimer? _timer;
    private int? _currentOpacity;
    
    private string LongestTitle => PreserveSpace ?
        _titles.OrderByDescending(x => x.Length).First() : _currentText;

    protected override void OnInitialized()
    {
        _timer = new(TimeSpan.FromSeconds(8));

        Task.Run(async () =>
        {
            try
            {
                await RunTimer();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        });
    }

    private async Task RunTimer()
    {
        while (await _timer!.WaitForNextTickAsync())
        {
            _currentOpacity = 0;
            await InvokeAsync(StateHasChanged);

            await Task.Delay(300);

            _index = (_index + 1) % _titles.Length;
            _currentText = _titles[_index];

            _currentOpacity = 1;
            await InvokeAsync(StateHasChanged);
        }
    }

    public void Dispose()
    {
        _timer?.Dispose();
    }
}