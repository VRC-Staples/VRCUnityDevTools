using UnityEditor;

namespace Staples.DevTools.Editor
{
    internal static class DevToolsMenuItems
    {
        [MenuItem("Tools/.Staples./Dev Tools/Show Current Project Binding")]
        public static void ShowCurrentProjectBinding()
        {
            ProjectBindingTool.ShowCurrentProjectBinding();
        }

        [MenuItem("Tools/.Staples./Dev Tools/Local or Embedded Package Switcher...")]
        public static void OpenPackageSwitcher()
        {
            VccPackageSwitcherWindow.Open();
        }
    }
}
