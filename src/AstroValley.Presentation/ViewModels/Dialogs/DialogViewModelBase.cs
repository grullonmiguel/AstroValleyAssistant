using CommunityToolkit.Mvvm.ComponentModel;

namespace AstroValley.Presentation.ViewModels.Dialogs;

public abstract partial class DialogViewModelBase : ObservableObject
{
    public string Title { get; protected set; } = string.Empty;
}
