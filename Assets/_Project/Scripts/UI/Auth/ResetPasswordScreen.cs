using System;
using LostPetNetwork.Services.Auth;
using UnityEngine;
using UnityEngine.UI;

namespace LostPetNetwork.UI
{
    /// <summary>Two-step reset: (1) request an emailed 6-digit code, (2) enter code + new password.</summary>
    public class ResetPasswordScreen : MonoBehaviour
    {
        [SerializeField] ScreenManager manager;
        [SerializeField] TextFieldView email;
        [SerializeField] GameObject codeStep;
        [SerializeField] TextFieldView code;
        [SerializeField] TextFieldView newPassword;
        [SerializeField] LoadingButton primaryButton;
        [SerializeField] MessageBanner banner;
        [SerializeField] Button resendLink;
        [SerializeField] Button backLink;

        bool _busy;
        bool _codeSent;

        void Awake()
        {
            primaryButton.Button.onClick.AddListener(OnPrimaryClicked);
            resendLink.onClick.AddListener(OnResendClicked);
            backLink.onClick.AddListener(() => manager.Show(ScreenId.SignIn));
        }

        void OnEnable() => ResetState();

        void ResetState()
        {
            _codeSent = false;
            banner.Hide();
            email.ClearError();
            code.ClearError();
            newPassword.ClearError();
            code.Clear();
            newPassword.Clear();
            email.SetInteractable(true);
            codeStep.SetActive(false);
            primaryButton.SetIdleText("Send code");
        }

        async void OnPrimaryClicked()
        {
            if (_busy) return;
            if (!_codeSent) await SendCodeAsync();
            else await ConfirmResetAsync();
        }

        async void OnResendClicked()
        {
            if (_busy) return;
            await SendCodeAsync();
        }

        async System.Threading.Tasks.Task SendCodeAsync()
        {
            banner.Hide();
            string emailError = AuthValidator.ValidateEmail(email.Text);
            email.SetError(emailError);
            if (emailError != null) return;

            _busy = true;
            primaryButton.SetLoading(true);
            try
            {
                var result = await AuthService.Instance.SendPasswordResetAsync(email.Text);
                if (this == null) return;
                if (!result.Ok) { banner.ShowError(result.Error); return; }

                _codeSent = true;
                email.SetInteractable(false);
                codeStep.SetActive(true);
                primaryButton.SetIdleText("Reset password");
                banner.ShowInfo($"If an account exists for {email.Text.Trim()}, we sent a 6-digit code. It expires soon.");
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                if (this != null) banner.ShowError("Something went wrong. Please try again.");
            }
            finally
            {
                _busy = false;
                if (this != null) primaryButton.SetLoading(false);
            }
        }

        async System.Threading.Tasks.Task ConfirmResetAsync()
        {
            banner.Hide();
            string codeError = AuthValidator.ValidateResetCode(code.Text);
            string passwordError = AuthValidator.ValidateNewPassword(newPassword.Text);
            code.SetError(codeError);
            newPassword.SetError(passwordError);
            if (codeError != null || passwordError != null) return;

            _busy = true;
            primaryButton.SetLoading(true);
            try
            {
                var result = await AuthService.Instance.ResetPasswordWithCodeAsync(email.Text, code.Text, newPassword.Text);
                if (this == null) return;
                if (!result.Ok) banner.ShowError(result.Error);
                // Success: the user is signed in; SessionChanged opens Home.
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                if (this != null) banner.ShowError("Something went wrong. Please try again.");
            }
            finally
            {
                _busy = false;
                if (this != null) primaryButton.SetLoading(false);
            }
        }
    }
}
