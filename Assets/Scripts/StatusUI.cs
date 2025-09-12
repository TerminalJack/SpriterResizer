using UnityEngine;
using UnityEngine.UIElements;

public class StatusUI : MonoBehaviour
{
    [SerializeField] private UIDocument _mainUIDocument;

    private UIDocument _uiDocument;
    private VisualElement _root;

    private Button _okButton;
    private ScrollView _scrollView;

    private VisualElement _mainUIRoot;

    private void Awake()
    {
        _uiDocument = GetComponent<UIDocument>();
        _root = _uiDocument.rootVisualElement;

        _mainUIRoot = _mainUIDocument.rootVisualElement;

        _okButton = _root.Q<Button>("OkButton");
        _scrollView = _root.Q<ScrollView>("MessageScrollView");

        _okButton.RegisterCallback<ClickEvent>(OnOkButtonClicked);
    }

    public void BeginResize()
    {
        _okButton.SetEnabled(false);
    }

    public void EndResize()
    {
        _okButton.SetEnabled(true);
    }

    public void ClearMessages()
    {
        _scrollView.Clear();
    }

    public void AddStatusMessage(string message)
    {
        var label = new Label(message)
        {
            style =
            {
                unityTextAlign = TextAnchor.MiddleLeft,
                marginTop = 1,
                marginBottom = 1,
                paddingLeft = 4,
                paddingTop = 0,
                paddingBottom = 0,
                fontSize = 10
            }
        };

        _scrollView.Add(label);

        // Wait for layout to complete before scrolling
        label.RegisterCallback<GeometryChangedEvent>(evt =>
        {
            _scrollView.ScrollTo(label);
        });
    }

    private void OnOkButtonClicked(ClickEvent evt)
    {
        _root.style.display = DisplayStyle.None;
        _mainUIRoot.style.display = DisplayStyle.Flex;
    }
}
