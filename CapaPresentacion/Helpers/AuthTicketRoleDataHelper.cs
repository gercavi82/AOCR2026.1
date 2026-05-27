using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace CapaPresentacion.Helpers
{
    public sealed class AuthTicketRoleData
    {
        public AuthTicketRoleData(IEnumerable<string> roles, string selectedRole)
        {
            Roles = (roles ?? Enumerable.Empty<string>())
                .Where(role => !string.IsNullOrWhiteSpace(role))
                .Select(role => role.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            SelectedRole = RoleGroupingHelper.NormalizeSelectedRole(selectedRole);
        }

        public IList<string> Roles { get; private set; }

        public string SelectedRole { get; private set; }
    }

    public static class AuthTicketRoleDataHelper
    {
        public const string SelectedRoleCookieName = "__AOCR_SelectedRole";
        private const string SelectedPrefix = "selected=";
        private const string RolesPrefix = "roles=";

        public static string Serialize(IEnumerable<string> roles, string selectedRole)
        {
            var roleList = (roles ?? Enumerable.Empty<string>())
                .Where(role => !string.IsNullOrWhiteSpace(role))
                .Select(role => role.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var normalizedSelectedRole = RoleGroupingHelper.NormalizeSelectedRole(selectedRole);
            if (string.IsNullOrWhiteSpace(normalizedSelectedRole) && roleList.Count > 0)
            {
                normalizedSelectedRole = RoleGroupingHelper.BuildUnifiedRoles(roleList).FirstOrDefault() ?? string.Empty;
            }

            return SelectedPrefix + Uri.EscapeDataString(normalizedSelectedRole ?? string.Empty)
                + ";"
                + RolesPrefix + string.Join("|", roleList.Select(role => Uri.EscapeDataString(role)));
        }

        public static AuthTicketRoleData Deserialize(string userData)
        {
            if (string.IsNullOrWhiteSpace(userData))
            {
                return new AuthTicketRoleData(Array.Empty<string>(), string.Empty);
            }

            if (userData.IndexOf(RolesPrefix, StringComparison.OrdinalIgnoreCase) < 0
                && userData.IndexOf(SelectedPrefix, StringComparison.OrdinalIgnoreCase) < 0)
            {
                return new AuthTicketRoleData(
                    userData.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries),
                    string.Empty);
            }

            var roles = new List<string>();
            var selectedRole = string.Empty;

            foreach (var chunk in userData.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var parts = chunk.Split(new[] { '=' }, 2);
                if (parts.Length != 2)
                {
                    continue;
                }

                var key = (parts[0] ?? string.Empty).Trim();
                var value = parts[1] ?? string.Empty;

                if (key.Equals("selected", StringComparison.OrdinalIgnoreCase))
                {
                    selectedRole = Uri.UnescapeDataString(value);
                    continue;
                }

                if (!key.Equals("roles", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                roles.AddRange(value
                    .Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(Uri.UnescapeDataString));
            }

            return new AuthTicketRoleData(roles, selectedRole);
        }

        public static string ReadSelectedRoleFromCookie(HttpCookieCollection cookies)
        {
            if (cookies == null)
            {
                return string.Empty;
            }

            var cookie = cookies[SelectedRoleCookieName];
            if (cookie == null || string.IsNullOrWhiteSpace(cookie.Value))
            {
                return string.Empty;
            }

            return RoleGroupingHelper.NormalizeSelectedRole(Uri.UnescapeDataString(cookie.Value));
        }
    }
}