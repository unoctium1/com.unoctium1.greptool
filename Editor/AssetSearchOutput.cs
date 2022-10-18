using UnityEditor;

namespace GrepTool
{
    public class AssetSearchOutput : SearchOutput
    {
        private const string FORMAT = "<color=green>{0}</color> {1} Files, ({2} Total Instances)";
        private readonly string _path;
        private readonly string _fileName;

        public AssetSearchOutput(string guid, int progressIDParent = -1, string path = null) : base(guid,
            progressIDParent)
        {
            _path = path ?? AssetDatabase.GUIDToAssetPath(guid);
            _fileName = GetFileName(_path);
        }

        protected override string ExportLine(IndividualSearchResult result)
        {
            return $"{_path},{result.Path},{result.NumResults},\"{result.Detail}\"";
        }

        protected override string GetFoldoutText()
        {
            return string.Format(FORMAT, _path, NumResults, TotalInstances);
        }

        protected override void Export()
        {
            GrepOutputWindow.SaveCSV("Export Asset Usage",
                "GrepOutput_" + _fileName,
                ExportTextAsCSV());
        }

        private static string GetFileName(string path)
        {
            var names = path.Split('/', '\\');
            var fileName = names[names.Length - 1];
            return fileName.Split('.')[0];
        }
    }
}



