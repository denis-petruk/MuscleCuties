using CommunityToolkit.Mvvm.Input;

namespace MuscleCuties.Core.ViewModels.Common;

public interface IPageLoadAware
{
    bool IsPageLoading { get; }
    bool IsLoadError { get; set; }
    AsyncRelayCommand LoadDataCommand { get; }
}
