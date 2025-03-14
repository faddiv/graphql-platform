using System.Reflection;

namespace GreenDonut.Data.Cursors.Serializers;

public interface ICursorKeySerializer
{
    bool IsSupported(Type type);

    MethodInfo GetCompareToMethod(Type type);

    object Parse(ReadOnlySpan<byte> formattedKey);

    bool TryFormat(object key, Span<byte> buffer, out int written);
}
