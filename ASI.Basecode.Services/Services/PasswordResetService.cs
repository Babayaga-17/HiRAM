using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Resources.Constants;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.Manager;
using ASI.Basecode.Services.ServiceModels;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Configuration;
using MimeKit;
using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using static ASI.Basecode.Resources.Constants.Enums;

namespace ASI.Basecode.Services.Services
{
    public class PasswordResetService : IPasswordResetService
    {
        private readonly IPasswordResetRepository _passwordResetRepository;
        private readonly IConfiguration _configuration;

        public PasswordResetService(IPasswordResetRepository passwordResetRepository, IConfiguration configuration)
        {
            _passwordResetRepository = passwordResetRepository;
            _configuration = configuration;
        }

        public async Task<SendResetCodeStatus> SendResetCodeAsync(string email)
        {
            var account = _passwordResetRepository.GetAccountByEmail(email?.Trim());
            if (account == null) return SendResetCodeStatus.AccountNotFound;

            var now = DateTime.UtcNow;
            var latestToken =
                _passwordResetRepository.GetLatestToken(account.AccountId);

            if (latestToken != null)
            {
                var resendAvailableAt = latestToken.CreatedAt.AddSeconds(PasswordResetPolicy.ResendCooldownSeconds);

                if (now < resendAvailableAt)
                    return SendResetCodeStatus.Cooldown;
            }

            var code = RandomNumberGenerator.GetInt32(100000, 1000000).ToString();

            var token = new PasswordResetToken
            {
                AccountId = account.AccountId,
                TokenHash = Hash(code),
                ExpiresAt = now.AddMinutes(PasswordResetPolicy.CodeExpiryMinutes),
                CreatedBy = "PasswordReset",
                CreatedAt = now
            };

            var cooldownCutoffUtc = now.AddSeconds(-PasswordResetPolicy.ResendCooldownSeconds);

            var tokenCreated = _passwordResetRepository.TryAddToken(token, cooldownCutoffUtc);

            if (!tokenCreated)
                return SendResetCodeStatus.Cooldown;

            var acceptedByMailServer = false;

            try
            {
                var message = new MimeMessage();
                message.From.Add(
                    MailboxAddress.Parse(_configuration["Smtp:From"]));
                message.To.Add(MailboxAddress.Parse(email.Trim()));
                message.Subject = "HiRAM password reset code";

                message.Body = new TextPart("plain")
                {
                    Text = $"Your HiRAM verification code is {code}. " +
                           $"It expires in {PasswordResetPolicy.CodeExpiryMinutes} minutes."
                };

                using var client = new SmtpClient();

                await client.ConnectAsync(_configuration["Smtp:Host"], int.Parse(_configuration["Smtp:Port"]), SecureSocketOptions.StartTls);
                await client.AuthenticateAsync(_configuration["Smtp:User"], _configuration["Smtp:Password"].Replace(" ", ""));
                await client.SendAsync(message);
                acceptedByMailServer = true;
                await client.DisconnectAsync(true);

                return SendResetCodeStatus.Sent;
            }
            catch (Exception)
            {
                // A disconnect failure must not invalidate an accepted message.
                if (acceptedByMailServer)
                    return SendResetCodeStatus.Sent;

                var failedAt = DateTime.UtcNow;

                token.ExpiresAt = failedAt;
                token.UpdatedAt = failedAt;
                token.UpdatedBy = "PasswordReset";

                _passwordResetRepository.Save();

                return SendResetCodeStatus.Failed;
            }
        }

        // Step 1: only checks the code. Does NOT use up the token yet.
        public int? VerifyCode(string email, string code)
        {
            var account = _passwordResetRepository.GetAccountByEmail(email?.Trim());
            if (account == null || string.IsNullOrWhiteSpace(code)) return null;

            var token = _passwordResetRepository.GetLatestToken(account.AccountId);

            if (token == null
                || token.UsedAt.HasValue
                || token.ExpiresAt <= DateTime.UtcNow
                || token.TokenHash != Hash(code.Trim()))
            {
                return null;
            }

            return token.TokenId;
        }

        // Step 2: saves the new password, then uses up the token.
        public bool SetNewPassword(string email, int tokenId, string newPassword)
        {
            var account = _passwordResetRepository.GetAccountByEmail(email?.Trim());
            if (account == null) return false;

            var token = _passwordResetRepository.GetLatestToken(account.AccountId);

            if (token == null
                || token.UsedAt.HasValue
                || token.ExpiresAt <= DateTime.UtcNow
                || token.TokenId != tokenId)
            {
                return false;
            }

            account.PasswordHash = PasswordManager.EncryptPassword(newPassword);
            account.FailedLoginAttempts = 0;
            account.LockoutEndAt = null;
            account.UpdatedBy = "PasswordReset";
            account.UpdatedAt = DateTime.UtcNow;

            token.UsedAt = DateTime.UtcNow;
            token.UpdatedBy = "PasswordReset";
            token.UpdatedAt = DateTime.UtcNow;

            _passwordResetRepository.Save();
            return true;
        }

        private static string Hash(string code)
        {
            using var sha = SHA256.Create();
            return Convert.ToHexString(sha.ComputeHash(Encoding.UTF8.GetBytes(code)));
        }

        public PasswordResetTimingViewModel GetTiming(string email)
        {
            var timing = new PasswordResetTimingViewModel
            {
                ServerNowUtc = DateTime.UtcNow
            };

            if (string.IsNullOrWhiteSpace(email))
                return timing;

            var account = _passwordResetRepository.GetAccountByEmail(email.Trim());

            if (account == null)
                return timing;

            var token = _passwordResetRepository.GetLatestToken(account.AccountId);

            if (token == null)
                return timing;

            timing.ResendAvailableAtUtc = DateTime.SpecifyKind(
                token.CreatedAt.AddSeconds(
                    PasswordResetPolicy.ResendCooldownSeconds),
                DateTimeKind.Utc);

            if (token.UsedAt == null)
            {
                timing.ExpiresAtUtc = DateTime.SpecifyKind(
                    token.ExpiresAt,
                    DateTimeKind.Utc);
            }

            return timing;
        }
    }
}
