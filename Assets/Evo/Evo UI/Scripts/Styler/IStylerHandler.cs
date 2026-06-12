namespace Evo.UI
{
    /// <summary>
    /// Interface to implement Styler to classes.
    /// </summary>
    public interface IStylerHandler
    {
        /// <summary>
        /// Points to the local variable.
        /// </summary>
        StylerPreset Preset { get; set; }

        /// <summary>
        /// The method to run when the preset changes.
        /// </summary>
        void UpdateStyler();
    }
}