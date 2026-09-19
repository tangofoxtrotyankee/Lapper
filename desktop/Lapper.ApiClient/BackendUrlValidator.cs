namespace Lapper.ApiClient;

/// <summary>
/// Validates the user-configurable backend URL. Plain http is allowed only
/// for loopback hosts; anything else must be https — captured screen text
/// must never travel unencrypted. Userinfo/query/fragment are rejected.
/// </summary>
public static class BackendUrlValidator
{
    public static bool TryValidate(string? input, out Uri? baseUrl)
    {
        baseUrl = null;
        if (string.IsNullOrWhiteSpace(input) ||
            !Uri.TryCreate(input.Trim(), UriKind.Absolute, out var uri))
        {
            return false;
        }

        if (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp)
        {
            return false;
        }
        if (uri.Scheme == Uri.UriSchemeHttp && !uri.IsLoopback)
        {
            return false;
        }
        if (uri.UserInfo.Length > 0 || uri.Query.Length > 0 || uri.Fragment.Length > 0)
        {
            return false;
        }

        baseUrl = uri;
        return true;
    }
}
