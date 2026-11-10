using System;
using System.Runtime.CompilerServices;

// Lets this assembly call into CombatSolver's internal types at runtime while we compile
// against the publicized reference copy (D:\work\refs\CombatSolver.dll).
[assembly: IgnoresAccessChecksTo("CombatSolver")]

namespace System.Runtime.CompilerServices
{
    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
    internal sealed class IgnoresAccessChecksToAttribute : Attribute
    {
        public IgnoresAccessChecksToAttribute(string assemblyName)
        {
            AssemblyName = assemblyName;
        }

        public string AssemblyName { get; }
    }
}
