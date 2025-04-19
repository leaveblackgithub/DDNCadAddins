/*
 * CommonAssemblyInfo.cs - 解决方案共享程序集信息
 * 
 * 功能：
 * 1. 为所有项目提供统一的版本号和程序集信息
 * 2. 使用通配符版本号(1.0.*)自动增加内部版本号
 * 3. 集中管理版权和公司信息
 * 
 * 注意：
 * 从2023年起，版本控制信息已迁移到Directory.props
 * 此文件仅作为向后兼容保留，新项目应直接使用Directory.props中的设置
 * 
 * 用法：
 * 在各项目的.csproj文件中通过Link引用此文件：
 * <Compile Include="$(SolutionDir)CommonAssemblyInfo.cs">
 *   <Link>Properties\CommonAssemblyInfo.cs</Link>
 * </Compile>
 */

using System.Runtime.InteropServices;

// Setting ComVisible to false makes the types in this assembly not visible
// to COM components.
[assembly: ComVisible(false)]

// 注意：版本信息现已移至Directory.props
// 下面的特性仅在不使用Directory.props的项目中生效
#if !USE_DIRECTORY_PROPS
// General Information about the solution
[assembly: AssemblyCompany("")]
[assembly: AssemblyProduct("AutoCAD_UnitTest")]
[assembly: AssemblyCopyright("Copyright © 2023")]
[assembly: AssemblyTrademark("")]
[assembly: AssemblyCulture("")]

// Version information
[assembly: AssemblyVersion("1.0.*")]
[assembly: AssemblyFileVersion("1.0.0.0")]
#endif
