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
    private readonly string _bergNamespace;
    private readonly string _releaseName;

    public KubernetesSecretKeyProvider(Kubernetes kubernetes)
    {
        _kubernetes = kubernetes;

        _bergNamespace = Environment.GetEnvironmentVariable("BERG_NAMESPACE") ?? "berg";
        _releaseName = Environment.GetEnvironmentVariable("BERG_RELEASE") ?? "berg";
        var secretName = $"{_releaseName[0..Math.Min(_releaseName.Length, 55)]}-openid";
        var secretsLoaded = false;
        var serverSigningRsa = RSA.Create(4096);
        var clientSigningRsa = RSA.Create(4096);
        ClientEncryptionKey = GenerateSymmetricSecurityKey();
        ServerEncryptionKey = GenerateSymmetricSecurityKey();
        do
        {
            try
            {
                var secret = kubernetes.ReadNamespacedSecret(secretName, _bergNamespace);
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
            if(!secretsLoaded) {
                try
                {
                    kubernetes.CreateNamespacedSecret(new k8s.Models.V1Secret
                    {
                        Metadata = new k8s.Models.V1ObjectMeta
                        {
                            Name = secretName
                        },
                        Data = new Dictionary<string, byte[]> {
                            { "clientEncryptionKey", ClientEncryptionKey.Key },
                            { "clientSigningKey", clientSigningRsa.ExportRSAPrivateKey() },
                            { "serverEncryptionKey", ServerEncryptionKey.Key },
                            { "serverSigningKey", serverSigningRsa.ExportRSAPrivateKey() },
                        }
                    }, _bergNamespace);
                }
                catch (HttpOperationException)
                {
                    Console.Error.WriteLine("Failed to write newly created openid keys");
                }
            }
        } while(!secretsLoaded);

        ClientSigningKey = new RsaSecurityKey(clientSigningRsa);
        ServerSigningKey = new RsaSecurityKey(serverSigningRsa);

        var protectSecretName = $"{_releaseName[0..Math.Min(_releaseName.Length, 55)]}-protect";
        var secretNames = _kubernetes.ListNamespacedSecret(_bergNamespace).Items.Select(s => s.Name()).ToHashSet();
        if(!secretNames.Contains(protectSecretName))
        {
            _kubernetes.CreateNamespacedSecret(new V1Secret
            {
                Metadata = new V1ObjectMeta
                {
                    Name = protectSecretName
                },
                Data = new Dictionary<string, byte[]>()
            }, _bergNamespace);
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
        var secretName = $"{_releaseName[0..Math.Min(_releaseName.Length, 55)]}-protect";
        var secret = _kubernetes.ReadNamespacedSecret(secretName, _bergNamespace);
        if (secret.Data != null) {
            foreach(var pair in secret.Data)
            {
                yield return XElement.Parse(Encoding.UTF8.GetString(Convert.FromBase64String(Encoding.UTF8.GetString(pair.Value))));
            }
        }
    }

    public void StoreElement(XElement element, string friendlyName)
    {
        var secretName = $"{_releaseName[0..Math.Min(_releaseName.Length, 55)]}-protect";
        var content = Convert.ToBase64String(Encoding.UTF8.GetBytes(element.ToString(SaveOptions.DisableFormatting)));
        var patch = $"{{\"stringData\": {{\"{friendlyName}\": \"{content}\"}}}}";
        _kubernetes.PatchNamespacedSecret(new V1Patch(patch, V1Patch.PatchType.MergePatch), secretName, _bergNamespace);
    }

    private static readonly RandomNumberGenerator RandomNumberGenerator = RandomNumberGenerator.Create();
    private static SymmetricSecurityKey GenerateSymmetricSecurityKey()
    {
        var key = new byte[32];
        RandomNumberGenerator.GetBytes(key);
        return new SymmetricSecurityKey(key);
    }
}
