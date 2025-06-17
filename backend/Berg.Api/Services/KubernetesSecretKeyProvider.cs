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
        var secretsLoaded = false;
        var serverSigningRsa = RSA.Create(4096);
        var clientSigningRsa = RSA.Create(4096);
        ClientEncryptionKey = GenerateSymmetricSecurityKey();
        ServerEncryptionKey = GenerateSymmetricSecurityKey();
        do
        {
            try
            {
                var secret = kubernetes.ReadNamespacedSecret(BergOpenIdSecretName, _kubernetesConfig.Namespace);
                ClientEncryptionKey = new SymmetricSecurityKey(secret.Data["clientEncryptionKey"]);
                clientSigningRsa.ImportRSAPrivateKey(secret.Data["clientSigningKey"], out _);
                ServerEncryptionKey = new SymmetricSecurityKey(secret.Data["serverEncryptionKey"]);
                serverSigningRsa.ImportRSAPrivateKey(secret.Data["serverSigningKey"], out _);
                secretsLoaded = true;
            }
            catch (HttpOperationException)
            {
                Console.Error.WriteLine("Unable to load existing openid secret keys, generating new ones");
            }
            if (!secretsLoaded)
            {
                try
                {
                    kubernetes.CreateNamespacedSecret(new V1Secret
                    {
                        Metadata = new V1ObjectMeta
                        {
                            Name = BergOpenIdSecretName
                        },
                        Data = new Dictionary<string, byte[]> {
                            { "clientEncryptionKey", ClientEncryptionKey.Key },
                            { "clientSigningKey", clientSigningRsa.ExportRSAPrivateKey() },
                            { "serverEncryptionKey", ServerEncryptionKey.Key },
                            { "serverSigningKey", serverSigningRsa.ExportRSAPrivateKey() },
                        }
                    }, _kubernetesConfig.Namespace);
                }
                catch (HttpOperationException ex)
                {
                    Console.Error.WriteLine("Failed to write newly created openid keys");
                    Console.Error.WriteLine(ex);
                }
            }
        } while (!secretsLoaded);

        ClientSigningKey = new RsaSecurityKey(clientSigningRsa);
        ServerSigningKey = new RsaSecurityKey(serverSigningRsa);

        var secretNames = _kubernetes.ListNamespacedSecret(_kubernetesConfig.Namespace).Items.Select(s => s.Name()).ToHashSet();
        if (!secretNames.Contains(BergProtectSecretName))
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
    }

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
