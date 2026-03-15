namespace BatchResizer.Models;

public enum OutputMode
{
    InPlace,         // Overwrite the original file
    Subfolder,       // Create a subfolder inside each source folder
    CustomFolder,    // Save all output to a single custom folder
    MirrorStructure, // Mirror source folder structure into a custom output folder
}
