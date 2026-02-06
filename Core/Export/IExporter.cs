namespace AstroValleyAssistant.Core.Export
{
    /// <summary>
    /// Standardized contract for exporting data to various destinations.
    /// </summary>
    /// <typeparam name="TData">The collection or object type to be exported.</typeparam>
    /// <typeparam name="TDestination">The target type (e.g., string for file paths).</typeparam>
    public interface IExporter<in TData, in TDestination>
    {
        /// <summary>
        /// Executes the export operation asynchronously.
        /// </summary>
        /// <param name="data">The data to process.</param>
        /// <param name="destination">The target location (can be null for system-level targets like Clipboard).</param>
        Task ExportAsync(TData data, TDestination? destination);
    }
}
