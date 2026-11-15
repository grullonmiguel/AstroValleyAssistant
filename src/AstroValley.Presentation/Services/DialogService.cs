using AstroValley.Presentation.Helpers;
using AstroValley.Presentation.Services.Interfaces;
using AstroValley.Presentation.ViewModels;
using MaterialDesignThemes.Wpf;

namespace AstroValley.Presentation.Services;

/// <summary>
/// Shows dialogs through Material Design's DialogHost (the one named <see cref="AppDefaults.RootDialogIdentifier"/>).
/// The view for each dialog view-model comes from the DataTemplates in Views/ViewTemplates.xaml.
/// </summary>
public sealed class DialogService : IDialogService
{
    // DialogHost allows a single open dialog, so queue concurrent requests.
    private readonly SemaphoreSlim _gate = new(1, 1);

    public async Task<TResult?> ShowAsync<TResult>(DialogViewModelBase<TResult> viewModel)
    {
        await _gate.WaitAsync();
        try
        {
            viewModel.AttachClose(result => DialogHost.Close(AppDefaults.RootDialogIdentifier, result));

            var result = await DialogHost.Show(viewModel, AppDefaults.RootDialogIdentifier);
            viewModel.OnDialogClosing(); // Esc or click-away: finishes the view-model's Result task too
            return result is TResult typed ? typed : default;
        }
        finally
        {
            _gate.Release();
        }
    }

    // shortcut: old overlay dialogs are not on DialogHost yet; migrate each caller to ShowAsync, then delete these.
    public bool IsDialogOpen => DialogHost.IsDialogOpen(AppDefaults.RootDialogIdentifier);

    public void ShowDialog(object dialogViewModel) =>
        throw new NotSupportedException($"{dialogViewModel.GetType().Name} has not been moved to the new dialog host yet (use ShowAsync).");

    public void CloseDialog() => DialogHost.Close(AppDefaults.RootDialogIdentifier);
}
