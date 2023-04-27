using System;

/// <summary>
/// Attribute for static data<br>
/// Previous method body will be cleared</br>
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
public class StaticDataAttribute : Attribute
{
    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="value">Raw <see cref="bool"/> data value</param>
    public StaticDataAttribute(params bool[] value) { }
    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="value">Raw <see cref="sbyte"/> data value</param>
    public StaticDataAttribute(params sbyte[] value) { }
    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="value">Raw <see cref="byte"/> data value</param>
    public StaticDataAttribute(params byte[] value) { }
    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="value">Raw <see cref="char"/> data value</param>
    public StaticDataAttribute(params char[] value) { }
    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="value">Raw <see cref="short"/> data value</param>
    public StaticDataAttribute(params short[] value) { }
    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="value">Raw <see cref="ushort"/> data value</param>
    public StaticDataAttribute(params ushort[] value) { }
    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="value">Raw <see cref="int"/> data value</param>
    public StaticDataAttribute(params int[] value) { }
    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="value">Raw <see cref="uint"/> data value</param>
    public StaticDataAttribute(params uint[] value) { }
    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="value">Raw <see cref="long"/> data value</param>
    public StaticDataAttribute(params long[] value) { }
    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="value">Raw <see cref="ulong"/> data value</param>
    public StaticDataAttribute(params ulong[] value) { }
    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="value">Raw <see cref="float"/> data value</param>
    public StaticDataAttribute(params float[] value) { }
    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="value">Raw <see cref="double"/> data value</param>
    public StaticDataAttribute(params double[] value) { }
}
