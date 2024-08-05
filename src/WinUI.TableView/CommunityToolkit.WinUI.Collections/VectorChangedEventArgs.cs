using Windows.Foundation.Collections;

namespace CommunityToolkit.WinUI.Collections;

/// <summary>
/// Vector changed EventArgs
/// </summary>
/// <remarks>
internal class VectorChangedEventArgs : IVectorChangedEventArgs
{
    /// Initializes a new instance of the <see cref="VectorChangedEventArgs"/> class.
    /// </remarks>
    /// <param name="cc">collection change type</param>
    /// <param name="index">index of item changed</param>
    /// <param name="item">item changed</param>
    public VectorChangedEventArgs(CollectionChange cc, int index = -1, object? item = null!)
    {
        CollectionChange = cc;
        Index = (uint)index;
        Item = item;
    }

    /// <summary>
    /// Gets the type of change that occurred in the vector.
    /// </summary>
    /// <returns>
    /// The type of change in the vector.
    /// </returns>
    public CollectionChange CollectionChange { get; }

    /// <summary>
    /// Gets the changed item.
    /// </summary>
    public object? Item { get; }

    /// <summary>
    /// Gets the position where the change occurred in the vector.
    /// </summary>
    /// <returns>
    /// The zero-based position where the change occurred in the vector, if applicable.
    /// </returns>
    public uint Index { get; }
}