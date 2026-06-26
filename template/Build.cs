using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml;
using UnityEditor;
using UnityEngine;
using Application = UnityEngine.Application;
using BuildResult = UnityEditor.Build.Reporting.BuildResult;
using System.Linq;

public class Build : MonoBehaviour
{
    static readonly string ProjectPath = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));

    static readonly string apkPath = Path.Combine(ProjectPath, "Builds/" + Application.productName + ".apk");

    private static readonly string androidExportPath =
        Path.GetFullPath(Path.Combine(ProjectPath, "../../android/UnityExport"));

    private static readonly string iosExportPath =
        Path.GetFullPath(Path.Combine(ProjectPath, "../../ios/UnityExport"));

    [MenuItem("Build/RN/Export Android Release %&i", false, 2)]
    public static void DoBuildAndroidRelease()
    {
        DoBuildAndroidLibrary(false);
    }

    [MenuItem("Build/RN/Export Android Debug %&d", false, 2)]
    public static void DoBuildAndroidDebug()
    {
        DoBuildAndroidLibrary(true);
    }

    public static void DoBuildAndroidLibrary(bool debug)
    {
        DoBuildAndroid(Path.Combine(apkPath, "unityLibrary"), debug);
    }

    public static void DoBuildAndroid(String buildPath, bool debug)
    {
        if (Directory.Exists(apkPath))
        {
            Directory.Delete(apkPath, true);
        }
        if (Directory.Exists(androidExportPath))
        {
            Directory.Delete(androidExportPath, true);
        }

        EditorUserBuildSettings.buildScriptsOnly = false;
        EditorUserBuildSettings.exportAsGoogleAndroidProject = true;
        EditorUserBuildSettings.androidBuildSystem = AndroidBuildSystem.Gradle;
        PlayerSettings.SetScriptingDefineSymbolsForGroup(BuildTargetGroup.Android, ""); // Clear out RUNNING_UNIT_TESTS if set

        var options = debug ?
            BuildOptions.Development | BuildOptions.AllowDebugging
            :
            BuildOptions.None;

        var report = BuildPipeline.BuildPlayer(
            GetEnabledScenes(),
            apkPath,
            BuildTarget.Android,
            options
        );

        if (report.summary.result != BuildResult.Succeeded)
        {
            throw new Exception("Build failed");
        }

        Copy(buildPath, androidExportPath);

        Copy(Path.Combine(apkPath, "shared"), System.IO.Path.Combine(androidExportPath, "shared/"),false);

        // Modify build.gradle
        var build_file = Path.Combine(androidExportPath, "build.gradle");
        if (System.IO.File.Exists(build_file))
        {
            var build_text = File.ReadAllText(build_file);
            build_text = build_text.Replace("../shared/", "./shared/");
            build_text = build_text.Replace("com.android.application", "com.android.library");
            build_text = build_text.Replace("bundle {", "splits {");
            build_text = build_text.Replace("enableSplit = false", "enable false");
            build_text = build_text.Replace("enableSplit = true", "enable true");
            build_text = build_text.Replace("implementation fileTree(dir: 'libs', include: ['*.jar'])",
                "implementation ':unity-classes'");
            build_text = Regex.Replace(build_text, @"\n.*applicationId '.+'.*\n", "\n");
            File.WriteAllText(build_file, build_text);
        }

        // Modify AndroidManifest.xml
        var manifest_file = Path.Combine(androidExportPath, "src/main/AndroidManifest.xml");
        if (System.IO.File.Exists(manifest_file))
        {
            var manifest_text = File.ReadAllText(manifest_file);
            manifest_text = Regex.Replace(manifest_text, @"<application .*>", "<application>");
            Regex regex = new Regex(@"<activity.*>(\s|\S)+?</activity>", RegexOptions.Multiline);
            manifest_text = regex.Replace(manifest_text, "");
            File.WriteAllText(manifest_file, manifest_text);
        }

        // Copy the launcher res directory
        Copy(Path.Combine(apkPath, "launcher/src/main/res"), Path.Combine(androidExportPath, "src/main/res"));

        // Copy gradle.properties
        File.Copy(Path.Combine(apkPath, "gradle.properties"), Path.Combine(androidExportPath, "gradle.properties"));

        // Modify strings.xml, if needed
        var strings_path = Path.Combine(androidExportPath, "src/main/res/values/strings.xml");
        XmlDocument strings_xml = new XmlDocument();
        strings_xml.Load(strings_path);

        bool alreadyHasStr = false;
        var allStrings = strings_xml.GetElementsByTagName("string");
        for (int i = 0; i < allStrings.Count; i++)
        {
            for (int j = 0; j < allStrings[i].Attributes.Count; j++)
            {
                if (allStrings[i].Attributes[j].Name == "name" && allStrings[i].Attributes[j].Value == "game_view_content_description")
                {
                    alreadyHasStr = true;
                }
            }
        }

        if (!alreadyHasStr)
        {
            var resources = strings_xml.GetElementsByTagName("resources");
            var nameElement = strings_xml.CreateElement("string");
            var nameData = strings_xml.CreateTextNode("Game view");
            nameElement.AppendChild(nameData);
            var nameAttr = strings_xml.CreateAttribute("name");
            nameAttr.Value = "game_view_content_description";
            nameElement.Attributes.Append(nameAttr);
            resources[0].AppendChild(nameElement);
            strings_xml.Save(strings_path);
        }

        // element[0].AppendChild(new XmlDocument.XmlNode)



        /*var strings_text = File.ReadAllText(strings_path);
        Regex regex_strings = new Regex(@"<string name=\""app_name\"".*>(\s|\S)+?</string>", RegexOptions.Multiline);
        var regex_match = regex_strings.Match(strings_text);
        regex_match.Success
        regex_
        var regex_strings_match = Regex.Match(strings_text, "");*/

        /*var strings_split = strings_text.Split('\n').ToList();
        var nameIdx = strings_split.IndexOf(strings_split.FirstOrDefault(x => x.ContainsInvariantCultureIgnoreCase("<string name=\"app_name\">")));
        strings_split.Insert(nameIdx, "  <string name=\"game_view_content_description\">Game view</string>");*/

        // var splitStrings = strings_text.Split("<string name=\"app_name\">");
        // Regex regex_strings = new Regex(@"<string name=\""app_name\"".*>(\s|\S)+?</string>", RegexOptions.Multiline);
        // manifest_text = regex.Replace(manifest_text, "");

    }

    [MenuItem("Build/RN/Export IOS Release %&i", false, 2)]
    public static void DoBuildIOSRelease()
    {
        DoBuildIOS(false);
    }

    [MenuItem("Build/RN/Export IOS Debug %&d", false, 2)]
    public static void DoBuildIOSDebug()
    {
        DoBuildIOS(true);
    }

    public static void DoBuildIOS(bool debug)
    {
        if (Directory.Exists(iosExportPath))
        {
            Directory.Delete(iosExportPath, true);
        }

        EditorUserBuildSettings.iOSXcodeBuildConfig = XcodeBuildConfig.Release;
        PlayerSettings.SetScriptingDefineSymbolsForGroup(BuildTargetGroup.iOS, ""); // Clear out RUNNING_UNIT_TESTS if set

        var options = debug ?
            BuildOptions.Development | BuildOptions.AllowDebugging | BuildOptions.EnableDeepProfilingSupport
            :
            BuildOptions.None;

        var report = BuildPipeline.BuildPlayer(
            GetEnabledScenes(),
            iosExportPath,
            BuildTarget.iOS,
            options
        );

        if (report.summary.result != BuildResult.Succeeded)
        {
            throw new Exception("Build failed");
        }

        XcodePostBuild.OnPostBuild(BuildTarget.iOS, iosExportPath);
    }

    static void Copy(string source, string destinationPath, bool deleteDestinationDirectoryIfExists = true)
    {
        if (deleteDestinationDirectoryIfExists)
        {
            if (Directory.Exists(destinationPath))
                Directory.Delete(destinationPath, true);

            Directory.CreateDirectory(destinationPath);
        }
        else
        {
            if (!Directory.Exists(destinationPath))
                Directory.CreateDirectory(destinationPath);
        }


        foreach (string dirPath in Directory.GetDirectories(source, "*",
                     SearchOption.AllDirectories)) {
			if(!Directory.Exists(dirPath.Replace(source, destinationPath))) {
            	Directory.CreateDirectory(dirPath.Replace(source, destinationPath));
            }
		}

        foreach (string newPath in Directory.GetFiles(source, "*.*",
            SearchOption.AllDirectories))
            File.Copy(newPath, newPath.Replace(source, destinationPath), true);
    }

    static string[] GetEnabledScenes()
    {
        var scenes = EditorBuildSettings.scenes
            .Where(s => s.enabled)
            .Select(s => s.path)
            .ToArray();

        return scenes;
    }
}
