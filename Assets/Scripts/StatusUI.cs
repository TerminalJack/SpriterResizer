using System;
using UnityEngine;
using UnityEngine.UIElements;

public class StatusUI : MonoBehaviour
{
    [HideInInspector] public Action OnOkButtonClicked;

    private UIDocument _uiDocument;
    private VisualElement _root;

    private Button _okButton;
    private ScrollView _scrollView;

    private void Awake()
    {
        _uiDocument = GetComponent<UIDocument>();
        _root = _uiDocument.rootVisualElement;

        _okButton = _root.Q<Button>("OkButton");
        _scrollView = _root.Q<ScrollView>("MessageScrollView");

        _okButton.RegisterCallback<ClickEvent>(HandleOkButtonClicked);
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

        _scrollView.schedule.Execute(() =>
        {
            var scroller = _scrollView.verticalScroller;
            scroller.value = scroller.highValue > 0 ? scroller.highValue : 0;
        }).StartingIn(delayMs: 20);
    }

    private void HandleOkButtonClicked(ClickEvent evt)
    {
        OnOkButtonClicked?.Invoke();
    }
}
