using System.Reflection;

[assembly: AssemblyTitle("Harvest Library")]
[assembly: AssemblyVersion("1.0.*")]
[assembly: AssemblyDescription("Harvest Library for LANDIS-II")]

#if DEBUG
[assembly: AssemblyConfiguration("Debug")]
#else
[assembly: AssemblyConfiguration("Release")]
#endif
