using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
using k8s;
using k8s.Autorest;
using k8s.Models;
using Microsoft.AspNetCore.DataProtection.Repositories;
using Microsoft.IdentityModel.Tokens;

namespace Berg.Api.Services;

public class KubernetesSecretKeyProvider : IXmlRepository
{
    public readonly SymmetricSecurityKey ClientEncryptionKey;
    public readonly RsaSecurityKey ClientSigningKey;
    public readonly SymmetricSecurityKey ServerEncryptionKey;
    public readonly RsaSecurityKey ServerSigningKey;
    private readonly Kubernetes _kubernetes;
    private readonly KubernetesClientConfiguration _kubernetesConfig;

    public const string BergOpenIdSecretName = "berg-openid";
    public const string BergProtectSecretName = "berg-protect";

    public KubernetesSecretKeyProvider(Kubernetes kubernetes, KubernetesClientConfiguration kubernetesConfig)
    {
        _kubernetes = kubernetes;
        _kubernetesConfig = kubernetesConfig;

        var clientSigningRsa = RSA.Create(4096);
        var serverSigningRsa = RSA.Create(4096);
        var clientEncryptionKey = GenerateSymmetricSecurityKey();
        var serverEncryptionKey = GenerateSymmetricSecurityKey();

        try
        {
            var secret = _kubernetes.ReadNamespacedSecret(BergOpenIdSecretName, _kubernetesConfig.Namespace);
            (clientSigningRsa, serverSigningRsa, clientEncryptionKey, serverEncryptionKey) = LoadOpenIdSecret(secret);
        }
        catch (HttpOperationException ex)
        {
            if (ex.Response?.StatusCode != HttpStatusCode.NotFound)
            {
                throw RbacError("read the OpenID key secret", BergOpenIdSecretName, ex);
            }

            // First boot in this cluster: no stored keys yet, so generate and store them
            try
            {
                _kubernetes.CreateNamespacedSecret(new V1Secret
                {
                    Metadata = new V1ObjectMeta
                    {
                        Name = BergOpenIdSecretName
                    },
                    Data = new Dictionary<string, byte[]>
                    {
                        { "clientEncryptionKey", clientEncryptionKey.Key },
                        { "clientSigningKey", clientSigningRsa.ExportRSAPrivateKey() },
                        { "serverEncryptionKey", serverEncryptionKey.Key },
                        { "serverSigningKey", serverSigningRsa.ExportRSAPrivateKey() },
                    }
                }, _kubernetesConfig.Namespace);
            }
            catch (HttpOperationException createEx)
            {
                // A concurrent replica may have created the secret in the meantime
                try
                {
                    var existing = _kubernetes.ReadNamespacedSecret(BergOpenIdSecretName, _kubernetesConfig.Namespace);
                    (clientSigningRsa, serverSigningRsa, clientEncryptionKey, serverEncryptionKey) = LoadOpenIdSecret(existing);
                }
                catch (HttpOperationException)
                {
                    // neither create nor re-read succeeded
                    throw RbacError("initialize the OpenID key secret", BergOpenIdSecretName, createEx);
                }
            }
        }

        EnsureDataProtectionSecret();

        ClientEncryptionKey = clientEncryptionKey;
        ServerEncryptionKey = serverEncryptionKey;
        ClientSigningKey = new RsaSecurityKey(clientSigningRsa);
        ServerSigningKey = new RsaSecurityKey(serverSigningRsa);
    }

    private void EnsureDataProtectionSecret()
    {
        try
        {
            _kubernetes.ReadNamespacedSecret(BergProtectSecretName, _kubernetesConfig.Namespace);
            return;
        }
        catch (HttpOperationException ex)
        {
            if (ex.Response?.StatusCode != HttpStatusCode.NotFound)
            {
                throw RbacError("read the data protection secret", BergProtectSecretName, ex);
            }
        }

        try
        {
            _kubernetes.CreateNamespacedSecret(new V1Secret
            {
                Metadata = new V1ObjectMeta
                {
                    Name = BergProtectSecretName
                },
                Data = new Dictionary<string, byte[]>()
            }, _kubernetesConfig.Namespace);
        }
        catch (HttpOperationException ex)
        {
            throw RbacError("create the data protection secret", BergProtectSecretName, ex);
        }
    }

    private static (RSA ClientSigning, RSA ServerSigning, SymmetricSecurityKey ClientEncryption, SymmetricSecurityKey ServerEncryption) LoadOpenIdSecret(V1Secret secret)
    {
        var clientSigning = RSA.Create();
        var serverSigning = RSA.Create();
        clientSigning.ImportRSAPrivateKey(secret.Data["clientSigningKey"], out _);
        serverSigning.ImportRSAPrivateKey(secret.Data["serverSigningKey"], out _);
        return (clientSigning, serverSigning,
            new SymmetricSecurityKey(secret.Data["clientEncryptionKey"]),
            new SymmetricSecurityKey(secret.Data["serverEncryptionKey"]));
    }

    private static InvalidOperationException RbacError(string action, string secretName, HttpOperationException inner) =>
        new InvalidOperationException(
            $"Failed to {action} '{secretName}' (HTTP {inner.Response?.StatusCode.ToString() ?? "no response"}).");

    public IReadOnlyCollection<XElement> GetAllElements()
    {
        try
        {
            return GetAllElementsCore().ToList().AsReadOnly();
        }
        catch (HttpOperationException)
        {
            return new List<XElement>().AsReadOnly();
        }
    }

    private IEnumerable<XElement> GetAllElementsCore()
    {
        var secret = _kubernetes.ReadNamespacedSecret(BergProtectSecretName, _kubernetesConfig.Namespace);
        if (secret.Data != null)
        {
            foreach (var pair in secret.Data)
            {
                yield return XElement.Parse(Encoding.UTF8.GetString(Convert.FromBase64String(Encoding.UTF8.GetString(pair.Value))));
            }
        }
    }

    public void StoreElement(XElement element, string friendlyName)
    {
        var content = Convert.ToBase64String(Encoding.UTF8.GetBytes(element.ToString(SaveOptions.DisableFormatting)));
        var patch = $"{{\"stringData\": {{\"{friendlyName}\": \"{content}\"}}}}";
        _kubernetes.PatchNamespacedSecret(new V1Patch(patch, V1Patch.PatchType.MergePatch), BergProtectSecretName, _kubernetesConfig.Namespace);
    }

    private static readonly RandomNumberGenerator RandomNumberGenerator = RandomNumberGenerator.Create();
    private static SymmetricSecurityKey GenerateSymmetricSecurityKey()
    {
        var key = new byte[32];
        RandomNumberGenerator.GetBytes(key);
        return new SymmetricSecurityKey(key);
    }
}
