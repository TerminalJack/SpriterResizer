using UnityEngine;
using UnityEngine.UIElements;
using System.IO;
using SFB;
using System.Collections;
using System;

public class MainUI : MonoBehaviour
{
    [SerializeField] private UIDocument _statusUIDocument;
    [SerializeField] private StatusUI _statusUI;

    [SerializeField] private Shader _bleedTransparentShader;
    [SerializeField] private Shader _blurShader;
    [SerializeField] private Shader _bicubicResizeShader;

    private UIDocument _uiDocument;
    private VisualElement _root;

    private Button _inputPathSelectionButton;
    private Button _outputPathSelectionButton;
    private Button _exitButton;
    private Button _resizeButton;

    private TextField _inputPathTextField;
    private TextField _outputPathTextField;

    private Label _inputPathValidLabel;
    private Label _outputPathValidLabel;

    private Slider _scalingFactorSlider;

    private string _inputPath;
    private string _outputPath;

    private VisualElement _statusRoot;

    private Material _bleedMat;
    private Material _blurMat;
    private Material _bicubicMat;

    private IEnumerator _resizeTask;
    private SpriterProjectResizer _projectResizer;

    bool IsInputPathValid => !string.IsNullOrEmpty(_inputPath) && File.Exists(_inputPath);

    bool IsOutputDirectoryValid =>
        !string.IsNullOrEmpty(_outputPath) &&
        Directory.Exists(Path.GetDirectoryName(_outputPath)) &&
        !IsOutputDirectorySameAsInputDirectory() &&
        _inputPath != _outputPath;

    bool OutputWillOverwriteExisting => IsOutputDirectoryValid && File.Exists(_outputPath);

    bool IsOutputDirectorySameAsInputDirectory()
    {
        if (!string.IsNullOrEmpty(_inputPath) && !string.IsNullOrEmpty(_outputPath))
        {
            var inputDirectory = Path.GetDirectoryName(_inputPath);
            var outputDirectory = Path.GetDirectoryName(_outputPath);

            var normalizedInputDirectory= Path.GetFullPath(inputDirectory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var normalizedOutputDirectory = Path.GetFullPath(outputDirectory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            return string.Equals(normalizedInputDirectory, normalizedOutputDirectory, StringComparison.OrdinalIgnoreCase);
        }

        return false;
    }

    private void Awake()
    {
        _uiDocument = GetComponent<UIDocument>();
        _root = _uiDocument.rootVisualElement;

        _statusRoot = _statusUIDocument.rootVisualElement;

        _root.style.display = DisplayStyle.Flex;
        _statusRoot.style.display = DisplayStyle.None;

        _inputPathSelectionButton = _root.Q<Button>("InputPathSelectionButton");
        _outputPathSelectionButton = _root.Q<Button>("OutputPathSelectionButton");
        _exitButton = _root.Q<Button>("ExitButton");
        _resizeButton = _root.Q<Button>("ResizeButton");

        _inputPathTextField = _root.Q<TextField>("InputPathTextField");
        _outputPathTextField = _root.Q<TextField>("OutputPathTextField");

        _inputPathValidLabel = _root.Q<Label>("InputPathValidLabel");
        _outputPathValidLabel = _root.Q<Label>("OutputPathValidLabel");

        _scalingFactorSlider = _root.Q<Slider>("ScalingFactorSlider");

        _inputPathSelectionButton.RegisterCallback<ClickEvent>(OnInputPathSelectionButtonClicked);
        _outputPathSelectionButton.RegisterCallback<ClickEvent>(OnOutputPathSelectionButtonClicked);
        _exitButton.RegisterCallback<ClickEvent>(OnExitButtonClicked);
        _resizeButton.RegisterCallback<ClickEvent>(OnResizeButtonClicked);

        _inputPathTextField.RegisterCallback<ChangeEvent<string>>(OnInputTextFieldChanged);
        _outputPathTextField.RegisterCallback<ChangeEvent<string>>(OnOutputTextFieldChanged);

        _inputPathValidLabel.style.display = DisplayStyle.None;
        _outputPathValidLabel.style.display = DisplayStyle.None;

        _bleedMat = new Material(_bleedTransparentShader);
        _blurMat = new Material(_blurShader);
        _bicubicMat = new Material(_bicubicResizeShader);

        UpdateResizeButtonStatus();
    }

    private void OnInputPathSelectionButtonClicked(ClickEvent evt)
    {
        string[] inputPaths = StandaloneFileBrowser.OpenFilePanel("Select Spriter Input File", "", "scml", false);
        if (inputPaths.Length > 0)
        {
            _inputPathTextField.value = inputPaths[0];
        }
    }

    private void OnOutputPathSelectionButtonClicked(ClickEvent evt)
    {
        string outputPath = StandaloneFileBrowser.SaveFilePanel("Save as Spriter Project (scml & images)", "", "", "scml");
        if (!string.IsNullOrEmpty(outputPath))
        {
            _outputPathTextField.value = outputPath;
        }
    }

    private void OnExitButtonClicked(ClickEvent evt)
    {
        Application.Quit();
    }

    private void OnStatusUIOkButtonClicked()
    {
        _root.style.display = DisplayStyle.Flex;
        _statusRoot.style.display = DisplayStyle.None;

        _statusUI.OnOkButtonClicked -= OnStatusUIOkButtonClicked;
    }

    private void OnResizeButtonClicked(ClickEvent evt)
    {
        float scalingFactor = _scalingFactorSlider.value;

        _resizeButton.SetEnabled(false);

        _root.style.display = DisplayStyle.None;
        _statusRoot.style.display = DisplayStyle.Flex;

        _statusUI.OnOkButtonClicked += OnStatusUIOkButtonClicked;

        _statusUI.ClearMessages();

        _statusUI.AddStatusMessage($"<color=#2020ff>Input path: {_inputPath}</color>");
        _statusUI.AddStatusMessage($"<color=#2020ff>Output path: {_outputPath}</color>");
        _statusUI.AddStatusMessage($"<color=#2020ff>Scaling factor: {scalingFactor}</color>");

        _statusUI.BeginResize();

        _projectResizer = new SpriterProjectResizer();
        _resizeTask = _projectResizer.Run(_inputPath, _outputPath, scalingFactor, _bleedMat, _blurMat, _bicubicMat);
    }

    private void Update()
    {
        if (_resizeTask == null)
        {
            return;
        }

        if (!_resizeTask.MoveNext())
        {
            _resizeTask = null;
            _projectResizer = null;

            _statusUI.AddStatusMessage("Resize complete.");
            _statusUI.EndResize();

            UpdateInputTextFieldStatus();
            UpdateOutputTextFieldStatus();
            UpdateResizeButtonStatus();
        }
        else if (_resizeTask.Current is string msg && !string.IsNullOrEmpty(msg))
        {
            _statusUI.AddStatusMessage(msg);
        }
    }

    private void UpdateResizeButtonStatus()
    {
        bool isEnabled = IsInputPathValid && IsOutputDirectoryValid;

        _resizeButton.SetEnabled(isEnabled);
    }

    private void OnInputTextFieldChanged(ChangeEvent<string> evt)
    {
        _inputPath = evt.newValue;

        UpdateInputTextFieldStatus();
        UpdateResizeButtonStatus();
    }

    private void UpdateInputTextFieldStatus()
    {
        _inputPathTextField.RemoveFromClassList("valid-field");
        _inputPathTextField.RemoveFromClassList("invalid-field");
        _inputPathTextField.RemoveFromClassList("neutral-field");

        if (!IsInputPathValid)
        {
            _inputPathTextField.AddToClassList("invalid-field");
            _inputPathValidLabel.style.display = DisplayStyle.Flex;
        }
        else
        {
            _inputPathTextField.AddToClassList("valid-field");
            _inputPathValidLabel.style.display = DisplayStyle.None;
        }
    }

    private void OnOutputTextFieldChanged(ChangeEvent<string> evt)
    {
        _outputPath = evt.newValue;

        UpdateOutputTextFieldStatus();
        UpdateResizeButtonStatus();
    }

    private void UpdateOutputTextFieldStatus()
    {
        _outputPathTextField.RemoveFromClassList("valid-field");
        _outputPathTextField.RemoveFromClassList("invalid-field");
        _outputPathTextField.RemoveFromClassList("neutral-field");

        if (IsOutputDirectorySameAsInputDirectory())
        {
            _outputPathTextField.AddToClassList("invalid-field");
            _outputPathValidLabel.text = "* The output directory cannot be the same as the input directory";
            _outputPathValidLabel.style.display = DisplayStyle.Flex;

        }
        else if (!IsOutputDirectoryValid)
        {
            _outputPathTextField.AddToClassList("invalid-field");
            _outputPathValidLabel.text = "* This field is invalid";
            _outputPathValidLabel.style.display = DisplayStyle.Flex;
        }
        else if (OutputWillOverwriteExisting)
        {
            _outputPathTextField.AddToClassList("valid-field");
            _outputPathValidLabel.text = "* This file will be overwritten";
            _outputPathValidLabel.style.display = DisplayStyle.Flex;
        }
        else
        {
            _outputPathTextField.AddToClassList("valid-field");
            _outputPathValidLabel.text = "";
            _outputPathValidLabel.style.display = DisplayStyle.None;
        }
    }
}
