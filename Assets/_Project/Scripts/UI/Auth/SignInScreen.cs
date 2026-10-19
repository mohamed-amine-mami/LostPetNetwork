using System;
using LostPetNetwork.Services.Auth;
using UnityEngine;
using UnityEngine.UI;

namespace LostPetNetwork.UI
{
    public class SignInScreen : MonoBehaviour
    {
        [SerializeField] ScreenManager manager;
        [SerializeField] TextFieldView email;
        [SerializeField] TextFieldView password;
        [SerializeField] LoadingButton signInButton;
        [SerializeField] MessageBanner banner;
        [SerializeField] Button forgotPasswordLink;
        [SerializeField] Button createAccountLink;

        bool _busy;

        void Awake()
        {
            signInButton.Button.onClick.AddListener(OnSignInClicked);
            forgotPasswordLink.onClick.AddListener(() => manager.Show(ScreenId.ResetPassword));
            createAccountLink.onClick.AddListener(() => manager.Show(ScreenId.SignUp));
        }

        void OnEnable()
        {
            banner.Hide();
            email.ClearError();
            password.ClearError();
            password.Clear();
        }

        public async void OnSignInClicked()
        {
            if (_busy) return;
            banner.Hide();
            string emailError = AuthValidator.ValidateEmail(email.Text);
            string passwordError = AuthValidator.ValidatePasswordForSignIn(password.Text);
            email.SetError(emailError);
            password.SetError(passwordError);
            if (emailError != null || passwordError != null) return;

            _busy = true;
            signInButton.SetLoading(true);
            try
            {
                var result = await AuthService.Instance.SignInAsync(email.Text, password.Text);
                if (this == null) return;
                if (!result.Ok) banner.ShowError(result.Error);
                // On success AuthService raises SessionChanged and ScreenManager opens Home.
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                if (this != null) banner.ShowError("Something went wrong. Please try again.");
            }
            finally
            {
                _busy = false;
                if (this != null) signInButton.SetLoading(false);
            }
        }
    }
}
