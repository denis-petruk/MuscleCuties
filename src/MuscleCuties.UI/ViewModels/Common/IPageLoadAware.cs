
namespace MuscleCuties.UI.ViewModels.Common;

/// <summary>
///     Contract a page reads to show the shared loading overlay while its first load runs.
/// </summary>
public interface IPageLoadAware
{
    bool IsPageLoading { get; }
    bool IsLoadError { get; set; }
    AsyncRelayCommand LoadDataCommand { get; }
}
