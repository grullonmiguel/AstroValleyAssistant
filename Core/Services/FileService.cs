namespace AstroValleyAssistant.Core.Services
{
    public class FileService : IFileService
    {
        public string? OpenFile(string title, string filter)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = title,
                Filter = filter
            };
            return dialog.ShowDialog() == true ? dialog.FileName : null;
        }
    }
}
