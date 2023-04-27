using Fody;
using InlineIL;
using System.Runtime.CompilerServices;

namespace StaticData.Fody
{
    internal static class Unsafe
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void CopyBlock(ref byte src, ref byte dest, int length)
        {
            IL.Push(ref dest);
            IL.Push(ref src);
            IL.Push(length);
            IL.Emit.Cpblk();
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static ref T UnBox<T>(object o) where T : struct
        {
            IL.Push(o);
            IL.Emit.Unbox<T>();
            return ref IL.ReturnRef<T>();
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static ref T Add<T>(ref T value, int offset)
        {
            IL.Push(ref value);
            IL.Push(offset);
            IL.Emit.Sizeof<T>();
            IL.Emit.Mul();
            IL.Emit.Add();
            return ref IL.ReturnRef<T>();
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static ref TDest As<TSrc, TDest>(ref TSrc src)
        {
            IL.Push(ref src);
            return ref IL.ReturnRef<TDest>();
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static T Cast<T>(this object src) where T : class
        {
            IL.Push(src);
            return IL.Return<T>();
        }
    }
}
