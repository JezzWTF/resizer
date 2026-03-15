namespace BatchResizer.Models;

public enum ResizeMode
{
    Fit,           // Fit within bounds, maintain aspect ratio (letterbox/pillarbox)
    Fill,          // Crop and fill to exact dimensions
    Stretch,       // Stretch to exact dimensions (may distort)
    LongestSide,   // Constrain longest side, maintain aspect ratio
    ShortestSide,  // Constrain shortest side, maintain aspect ratio
    Percentage,    // Scale by percentage
}
