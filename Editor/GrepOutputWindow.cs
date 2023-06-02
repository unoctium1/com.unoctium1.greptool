using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor.UIElements;

namespace GrepTool
{
    public class GrepOutputWindow : EditorWindow
    {
        private const string CSV_HEADER_ROW = "Search Path,Result Path,Instances Per Result,Full Result\n";

        private const string ROOT_PATH = "Packages/com.unoctium1.greptool";
        
        private const string UXML_PATH =
            ROOT_PATH + "/Editor/Assets/GrepOutputWindow.uxml";

        private const string UXML_ELEMENT_PATH =
            ROOT_PATH + "/Editor/Assets/GrepOutputElement.uxml";

        private readonly StyleEnum<DisplayStyle> _visible = new StyleEnum<DisplayStyle>(DisplayStyle.Flex);
        private readonly StyleEnum<DisplayStyle> _hidden = new StyleEnum<DisplayStyle>(DisplayStyle.None);

        private Button _searchBtn;
        private Button _exportBtn;
        private TextField _searchField;
        private VisualElement _contentHolder;
        private VisualElement _placeholder;

        private VisualTreeAsset _outputElement;

        private string[] _searchTerms;
        private SearchOutput[] _results;

        private bool _isCurrentSearchGUIDs = false;

        [MenuItem("Window/GrepOutputWindow")]
        public static void ShowWindow()
        {
            GrepOutputWindow wnd = GetWindow<GrepOutputWindow>();
            wnd.titleContent = new GUIContent("GrepOutputWindow");
        }

        [MenuItem("Assets/Print asset usages")]
        public static void PrintAssetUsages()
        {
            GrepOutputWindow wnd = GetWindow<GrepOutputWindow>();
            wnd.titleContent = new GUIContent("GrepOutputWindow");
            wnd.SetupSearch(Selection.assetGUIDs, true);
        }

        [MenuItem("Assets/Print asset usages", true)]
        public static bool PrintAssetUsagesValidation()
        {
            return Selection.count > 0;
        }

        public static void SaveCSV(string title, string defaultName, string csvText)
        {
            var path = EditorUtility.SaveFilePanel(title,
                Application.dataPath,
                $"{defaultName}_{DateTime.Now.ToFileTimeUtc()}",
                "csv");
            File.WriteAllText(path, GrepOutputWindow.CSV_HEADER_ROW + csvText);
        }

        public void CreateGUI()
        {
            VisualElement root = rootVisualElement;

            var visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UXML_PATH);
            _outputElement = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UXML_ELEMENT_PATH);
            visualTree.CloneTree(rootVisualElement);

            _searchBtn = root.Q<Button>("searchBtn");
            _exportBtn = root.Q<Button>("exportBtn");
            _searchField = root.Q<TextField>("searchField");
            _contentHolder = root.Q<VisualElement>("contentHolder");
            _placeholder = root.Q<VisualElement>("placeholderTxt");

            _searchBtn.clicked += Search;
            _exportBtn.clicked += ExportAll;
            _searchField.RegisterValueChangedCallback(_ => InvalidateOnlyGUIDSearch());

            _placeholder.style.display = _hidden;
        }

        private async void SetupSearch(string[] searchTerms, bool isGUIDs = false)
        {
            _contentHolder.Clear();
            _placeholder.style.display = _visible;
            _searchTerms = searchTerms;
            _results = new SearchOutput[searchTerms.Length];
            _isCurrentSearchGUIDs = isGUIDs;

            if (isGUIDs)
            {
                _searchField.SetValueWithoutNotify(GuidsToSearchTerms(searchTerms));
            }

            int progressId = Progress.Start("Grep Search");

            for (int i = 0; i < searchTerms.Length; i++)
            {
                _results[i] = isGUIDs
                    ? new AssetSearchOutput(_searchTerms[i], progressId)
                    : SearchOutput.GetOutput(_searchTerms[i], progressId);
            }

            try
            {
                await Task.WhenAll(_results.Select(result => result.GetResults()));
                _placeholder.style.display = _hidden;
                foreach (var result in _results)
                {
                    _contentHolder.Add(result.GetOutput(_outputElement));
                }
            }
            catch (Exception e)
            {
                Progress.Finish(progressId, Progress.Status.Failed);
                Debug.LogWarning(
                    "An error occurred in running the grep window, double check that Git is installed and accessible on your path\n" +
                    e.Message);
            }
        }

        private void Search()
        {
            var search = _searchField.value;
            if (string.IsNullOrEmpty(search))
                return;
            SetupSearch(GenerateSearchTerms(search), _isCurrentSearchGUIDs);
        }

        private void ExportAll()
        {
            if (_results == null || _results.Length < 1)
            {
                return;
            }

            var stringBuilder = new StringBuilder();
            foreach (var result in _results)
            {
                stringBuilder.AppendLine(result.ExportTextAsCSV());
            }

            SaveCSV("Export Asset Usages", "GrepOutput", stringBuilder.ToString());
        }

        private void InvalidateOnlyGUIDSearch()
        {
            _isCurrentSearchGUIDs = false;
        }

        private static string GuidsToSearchTerms(string[] guids)
        {
            return string.Join(",", guids);
        }

        private static string[] GenerateSearchTerms(string search)
        {
            return search.Split(',');
        }
    }
}