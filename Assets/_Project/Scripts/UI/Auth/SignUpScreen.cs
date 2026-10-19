using System;
using LostPetNetwork.Services.Auth;
using UnityEngine;
using UnityEngine.UI;

namespace LostPetNetwork.UI
{
    public class SignUpScreen : MonoBehaviour
    {
        [SerializeField] ScreenManager manager;
        [SerializeField] TextFieldView displayName;
        [SerializeField] TextFieldView email;
        [SerializeField] TextFieldView password;
        [SerializeField] LoadingButton createButton;
        [SerializeField] MessageBanner banner;
        [SerializeField] Button signInLink;

        bool _busy;

        void Awake()
        {
            createButton.Button.onClick.AddListener(OnCreateClicked);
            signInLink.onClick.AddListener(() => manager.Show(ScreenId.SignIn));
        }

        void OnEnable()
        {
            banner.Hide();
            displayName.ClearError();
            email.ClearError();
            password.ClearError();
            password.Clear();
        }

        public async void OnCreateClicked()
        {
            if (_busy) return;
            banner.Hide();
            string nameError = AuthValidator.ValidateDisplayName(displayName.Text);
            string emailError = AuthValidator.ValidateEmail(email.Text);
            string passwordError = AuthValidator.ValidateNewPassword(password.Text);
            displayName.SetError(nameError);
            email.SetError(emailError);
            password.SetError(passwordError);
            if (nameError != null || emailError != null || passwordError != null) return;

            _busy = true;
            createButton.SetLoading(true);
            try
            {
                var result = await AuthService.Instance.SignUpAsync(displayName.Text, email.Text, password.Text);
                if (this == null) return;
                if (!result.Ok)
                    banner.ShowError(result.Error);
                else if (result.NeedsEmailConfirmation)
                {
                    password.Clear();
                    banner.ShowInfo($"Almost there! We sent a confirmation link to {email.Text.Trim()}. Open it, then come back and sign in.");
                }
                // Without email confirmation, SessionChanged opens Home automatically.
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                if (this != null) banner.ShowError("Something went wrong. Please try again.");
            }
            finally
            {
                _busy = false;
                if (this != null) createButton.SetLoading(false);
            }
        }
    }
}
