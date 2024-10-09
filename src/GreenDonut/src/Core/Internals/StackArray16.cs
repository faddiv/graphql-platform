using System.Runtime.CompilerServices;

namespace GreenDonut.Internals;

[InlineArray(16)]
internal struct StackArray16<T>
{
    public T first;
}
