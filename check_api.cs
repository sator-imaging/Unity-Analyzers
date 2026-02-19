using Microsoft.CodeAnalysis;
using System;

public class Test
{
    public void M(ITypeSymbol type)
    {
        var x = type.TypeKind;
        // var y = type.IsReadOnly; // This should fail if it's not on ITypeSymbol
    }
}
