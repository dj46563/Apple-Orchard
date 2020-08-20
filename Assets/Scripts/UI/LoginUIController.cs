using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

public class LoginUIController : MonoBehaviour
{
    // Raised when login is successful, arguments are the username and the hash
    public static Action<string, string, Color> LoginSucceeded;
    
    [SerializeField] private InputField _displayName;
    [SerializeField] private InputField _password;
    [SerializeField] private Button LoginButton;
    [SerializeField] private Button RegisterButton;
    [SerializeField] private Toggle RemmemberMe;
    [SerializeField] private FlexibleColorPicker ColorPicker;

    private bool _usernameValid = false;
    private bool _passwordValid = false;

    private bool _loginSelected = true;

    // Start is called before the first frame update
    void Start()
    {
        LoginButton.onClick.AddListener(OnLoginClick);
        RegisterButton.onClick.AddListener(OnRegisterClick);
        
        _displayName.onValueChanged.AddListener(ValidateUsername);
        _password.onValueChanged.AddListener(ValidatePassword);
        
        SetLoginAndRegisterInteractable(_usernameValid, _passwordValid);
        
        LoginSucceeded += OnLoginSuccess;
        
        _displayName.Select();

        if (PlayerPrefs.HasKey("Username") && PlayerPrefs.HasKey("Password"))
        {
            _displayName.text = PlayerPrefs.GetString("Username");
            _password.text = PlayerPrefs.GetString("Password");
        }
    }

    private void OnLoginSuccess(string username, string hash, Color color)
    {
        if (RemmemberMe.isOn)
        {
            PlayerPrefs.SetString("Username", _displayName.text);
            PlayerPrefs.SetString("Password", _password.text);
        }
        else
        {
            PlayerPrefs.DeleteKey("Username");
            PlayerPrefs.DeleteKey("Password");
        }
    }

    private void ValidatePassword(string password)
    {
        _passwordValid = password.Length > 6 &&
                         password.Length < 30;
        
        SetLoginAndRegisterInteractable(_usernameValid, _passwordValid);
    }

    private void ValidateUsername(string username)
    {
        _usernameValid = username.Length > 3 &&
                         username.Length < 18;

        SetLoginAndRegisterInteractable(_usernameValid, _passwordValid);
    }

    private void SetLoginAndRegisterInteractable(bool userValid, bool passwordValid)
    {
        if (userValid && passwordValid)
        {
            LoginButton.interactable = true;
            RegisterButton.interactable = true;
        }
        else
        {
            LoginButton.interactable = false;
            RegisterButton.interactable = false;
        }
    }

    private void OnRegisterClick()
    {
        // Send the HTTP Request
        StartCoroutine(Register(_displayName.text, _password.text));
    }

    private void OnLoginClick()
    {
        // Send the HTTP Request
        StartCoroutine(Login(_displayName.text, _password.text, LoginSucceeded));
    }

    private IEnumerator Register(string username, string password)
    {
        WWWForm form = new WWWForm();
        form.AddField("username", username);
        form.AddField("password", password);

        using (UnityWebRequest www = UnityWebRequest.Post(Constants.PHPServerHost + "/register.php", form))
        {
            // Wait for request to come back
            yield return www.SendWebRequest();

            if (www.isNetworkError || www.isHttpError)
            {
                Debug.Log(www.error + www.downloadHandler.text);
            }
            else
            {
                Debug.Log("Registration complete");
            }
        }
    }

    private IEnumerator Login(string username, string password, Action<string, string, Color> successEvent)
    {
        WWWForm form = new WWWForm();
        form.AddField("username", username);
        form.AddField("password", password);

        using (UnityWebRequest www = UnityWebRequest.Post(Constants.PHPServerHost + "/login.php", form))
        {
            // Wait for request to come back
            yield return www.SendWebRequest();

            if (www.isNetworkError || www.isHttpError)
            {
                Debug.Log(www.error + www.downloadHandler.text);
            }
            else
            {
                string hash = www.downloadHandler.text;
                Debug.Log("Login complete");
                successEvent.Invoke(username, hash, ColorPicker.color);
            }
        }
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Tab))
        {
            if (_loginSelected)
            {
                _password.Select();
                _loginSelected = false;
            }
            else
            {
                _displayName.Select();
                _loginSelected = true;
            }
        }

        if (Input.GetKeyDown(KeyCode.Return) && LoginButton.interactable)
        {
            LoginButton.onClick.Invoke();
        }
    }
}
