using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace GrepTool
{
    public class SearchOutput
    {
        private const string FOLDOUT_NAME = "outputElementFoldout";
        private const string BUTTON_NAME = "exportBtn";

        private const string FORMAT = "<color=blue>{0}</color> {1} Files, ({2} Total Instances)";
        private const int LINE_HEIGHT = 16;
        private const float MAX_LIST_HEIGHT = 512f;

        private readonly string _searchTerm;

        private readonly GrepProcess _process;

        private Foldout _foldout;
        private Button _button;

        protected int NumResults => Results?.Count ?? -1;

        protected int TotalInstances => Results.Values.Aggregate(0, (val, result) => val + result.NumResults);
        protected Dictionary<string, IndividualSearchResult> Results { get; private set; }

        public static SearchOutput GetOutput(string searchTerm, int progressIDParent = -1)
        {
            var path = AssetDatabase.GUIDToAssetPath(searchTerm);
            return string.IsNullOrEmpty(path)
                ? new SearchOutput(searchTerm, progressIDParent)
                : new AssetSearchOutput(searchTerm, progressIDParent, path);
        }

        protected SearchOutput(string search, int progressIDParent)
        {
            _searchTerm = search;
            _process = new GrepProcess(search, AddLine, progressIDParent);
        }

        public async Task GetResults()
        {
            Results = new Dictionary<string, IndividualSearchResult>();
            await _process.StartAsync();
        }

        public VisualElement GetOutput(VisualTreeAsset template)
        {
            var parentElement = template.Instantiate();
            _foldout = parentElement.Q<Foldout>(FOLDOUT_NAME);
            _button = parentElement.Q<Button>(BUTTON_NAME);

            _foldout.text = GetFoldoutText();
            _foldout.value = false;

            var values = Results.Values.ToList();
            var list = new ListView(values, LINE_HEIGHT, () => new Label(),
                (element, i) => { ((Label) element).text = values[i].ToString(); })
            {
                selectionType = SelectionType.Single,
                showBorder = true,
                showAlternatingRowBackgrounds = AlternatingRowBackground.ContentOnly,
                style =
                {
                    height = GetListViewSize(MAX_LIST_HEIGHT),
                    flexGrow = 1.0f
                }
            };
            _button.clicked += Export;
            list.onItemsChosen += objs => ((IndividualSearchResult) objs.First()).FocusOnAsset();
            _foldout.contentContainer.Add(list);
            return parentElement;
        }

        public string ExportTextAsCSV()
        {
            StringBuilder text = new StringBuilder();
            foreach (var result in Results.Values)
            {
                text.AppendLine(ExportLine(result));
            }

            return text.ToString();
        }

        protected virtual string ExportLine(IndividualSearchResult result)
        {
            return $"{_searchTerm},{result.Path},{result.NumResults},\"{result.Detail}\"";
        }

        protected virtual string GetFoldoutText()
        {
            return string.Format(FORMAT, _searchTerm, NumResults, TotalInstances);
        }

        protected virtual void Export()
        {
            GrepOutputWindow.SaveCSV("Export Asset Usage",
                "GrepOutput_" + _searchTerm,
                ExportTextAsCSV());
        }

        private void AddLine(string output)
        {
            if (string.IsNullOrEmpty(output)) return;
            var path = IndividualSearchResult.FilterPath(output, out var detail);
            if (Results.TryGetValue(path, out var result))
            {
                result.AddToResult(detail);
                Results[path] = result;
            }
            else
            {
                Results.Add(path, new IndividualSearchResult(detail, path));
            }

        }

        private float GetListViewSize(float maxSize)
        {
            return Mathf.Min(maxSize, NumResults * LINE_HEIGHT);
        }

        protected struct IndividualSearchResult
        {
            public string Path { get; private set; }
            public string Detail { get; private set; }
            public int NumResults { get; private set; }

            public IndividualSearchResult(string detail, string path)
            {
                Detail = detail;
                Path = path;
                NumResults = 1;
            }

            public override string ToString()
            {
                return $"{Path}: {NumResults} usages, {Detail}";
            }

            public void AddToResult(string detail)
            {
                Detail += ", " + detail;
                NumResults++;
            }

            public static string FilterPath(string result, out string detail)
            {
                var colonIndex = result.IndexOf(':');
                detail = result.Substring(colonIndex + 1);
                return result.Substring(0, colonIndex);
            }

            public void FocusOnAsset()
            {
                var obj = AssetDatabase.LoadAssetAtPath<Object>(Path);
                if (obj != null)
                {
                    Selection.activeObject = obj;
                }
            }
        }
    }
}
