using InlineIL;
using System;

namespace StaticData.Fody
{
    public unsafe partial class ModuleWeaver
    {
        class TypeData
        {
            private static void GenericUnBoxer<T>(object o, ref byte pointer) where T : struct
            {
                Unsafe.As<byte, T>(ref pointer) = Unsafe.UnBox<T>(o);
            }
            internal readonly int ByteSize;
            internal readonly delegate*<object, ref byte, void> BoxFunction;
            private TypeData(int byteSize, delegate*<object, ref byte, void> boxFunction)
            {
                ByteSize = byteSize;
                BoxFunction = boxFunction;
            }
            internal static TypeData Create<T>() where T : unmanaged
            {
                return new TypeData(sizeof(T), &GenericUnBoxer<T>);
            }
        }
    }
}