using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>Submits an InputField with the specified button.</summary>
//Prevents MonoBehaviour of same type (or subtype) to be added more than once to a GameObject.
[DisallowMultipleComponent]
//This automatically adds required components as dependencies.
[RequireComponent(typeof(InputField))]
public class SubmitOnEnter : MonoBehaviour
{
    public bool trimWhitespace = true;

    [Serializable]
    public class TextSubmitEvent : UnityEvent<string>
    {
    }

    [SerializeField] private TextSubmitEvent onTextSubmit = new TextSubmitEvent();

    bool allowEnter;
    InputField _inputField;

    void Start()
    {
        _inputField = GetComponent<InputField>();
        _inputField.onEndEdit.AddListener(delegate(string arg0)
        {
            ValidateAndSubmit(_inputField.text);
            _inputField.text = "";
        }); 
    }

    void Update()
    {
        if (allowEnter && (_inputField.text.Length > 0) &&
            (Input.GetKey(KeyCode.Return) || Input.GetKey(KeyCode.KeypadEnter)))
        {
            ValidateAndSubmit(_inputField.text);
            _inputField.text = "";
            allowEnter = false;
        }
        else
            allowEnter = _inputField.isFocused;
    }


    bool isInvalid(string fieldValue)
    {
        // change to the validation you want
        return string.IsNullOrEmpty(fieldValue);
    }

    void ValidateAndSubmit(string fieldValue)
    {
        if (isInvalid(fieldValue))
            return;
        // change to whatever you want to run when user submits
        onTextSubmit?.Invoke(fieldValue);
    }

    // to be called from a submit button onClick event
    public void ValidateAndSubmit()
    {
        ValidateAndSubmit(_inputField.text);
    }
}