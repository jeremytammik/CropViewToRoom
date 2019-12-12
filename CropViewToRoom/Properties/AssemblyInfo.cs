using System.Reflection;
using System.Runtime.InteropServices;

// General Information about an assembly is controlled through the following
// set of attributes. Change these attribute values to modify the information
// associated with an assembly.
[assembly: AssemblyTitle( "CropViewToRoom" )]
[assembly: AssemblyDescription( "Revit .NET Add-In that, for each level, collects all rooms and generates views cropped to each room" )]
[assembly: AssemblyConfiguration( "" )]
[assembly: AssemblyCompany( "Autodesk Inc." )]
[assembly: AssemblyProduct( "CropViewToRoom Revit C# .NET Add-In" )]
[assembly: AssemblyCopyright( "Copyright 2019 (C) Jeremy Tammik, Autodesk Inc." )]
[assembly: AssemblyTrademark( "" )]
[assembly: AssemblyCulture( "" )]

// Setting ComVisible to false makes the types in this assembly not visible
// to COM components.  If you need to access a type in this assembly from
// COM, set the ComVisible attribute to true on that type.
[assembly: ComVisible( false )]

// The following GUID is for the ID of the typelib if this project is exposed to COM
[assembly: Guid( "321044f7-b0b2-4b1c-af18-e71a19252be0" )]

// Version information for an assembly consists of the following four values:
//
//      Major Version
//      Minor Version
//      Build Number
//      Revision
//
// You can specify all the values or you can default the Build and Revision Numbers
// by using the '*' as shown below:
// [assembly: AssemblyVersion("1.0.*")]
//
// History:
//
// 2019-11-22 2020.0.0.0 successful test with zero offset
// 2019-12-04 2020.0.0.1 integrated two updates by Stephen
// 2019-12-12 2020.0.0.2 integrated new code and sample models by Stephen from new thread https://forums.autodesk.com/t5/revit-api-forum/createviaoffset-curveloop-original-ilist-lt-double-gt/m-p/9199151
//
[assembly: AssemblyVersion( "2020.0.0.2" )]
[assembly: AssemblyFileVersion( "2020.0.0.2" )]
