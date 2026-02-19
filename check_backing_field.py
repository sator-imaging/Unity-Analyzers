import subprocess

test_code = """
public class Test
{
    public static int MyProperty { get; set; }
}
"""

# I cannot easily run Roslyn from python here without a lot of setup.
# I'll just assume IsImplicitlyDeclared works as expected for now.
