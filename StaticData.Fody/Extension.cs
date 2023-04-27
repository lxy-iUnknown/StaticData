using Mono.Cecil;
using Mono.Collections.Generic;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace StaticData.Fody
{
    internal static class Extension
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void ForEach<T>(this Collection<T> collection, Action<T> action)
        {
            var enumerator = collection.GetEnumerator();
            while (enumerator.MoveNext())
            {
                action(enumerator.Current);
            }
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool TryGet<T>(this Collection<T> collection, Func<T, bool> predicate, out T result)
        {
            var enumerator = collection.GetEnumerator();
            while (enumerator.MoveNext())
            {
                var item = enumerator.Current;
                if (predicate(item))
                {
                    result = item;
                    return true;
                }
            }
            result = default;
            return false;
        }
    }
}
