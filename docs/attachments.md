---
icon: lucide/paperclip
---

Berg accepts two types of attachments for challenges. You can either use one shared container with a web server serving all handouts or publishing each as an OCI image.

### Dedicated container for handouts
The simplest setup can be achieved by deploying a static docker image containing a webserver that hosts challenge handouts.

This can be done with a simple Nginx image:
```dockerfile
FROM nginx:latest

COPY handouts/ /usr/share/nginx/html/handouts/
```

Then, you can set `handout.enabled: true` in the chart, update `handout.image` and add handouts to challenges:
```yaml
  attachments:
    - fileName: example-web-rce.tar.gz
      downloadUrl: /handouts/example-web-rce.tar.gz
```

For an automated setup that packages all handouts into an image, see [`create-handouts.py`](https://github.com/BergCTF/platform/blob/main/challenges/create-handouts.py) in the repository.

### Using OCI images
While having a single container for all handouts might be easy for smaller setups, it has its downsides:

* You cannot update handouts without a downtime
* You need to rebuild all handouts whenever one changes
* The container size can grow quite large the more challenges you add

This is where OCI images come into play. We can create an image per handout and publish them to a registry, where Berg will pull them from.

Use a tool like [`oras`](https://github.com/oras-project/oras) to build and push the handouts to the registry. We recommend following a naming scheme, e.g. packaging all handouts as `<challengename>.tar.gz`.

Then, you can use them inside a challenge manifest as follows:
```yaml
  attachments:
    - fileName: hello-world.tar.gz
      downloadImage: your-registry.local/challenges/handouts/web/hello-world:latest
```

Berg will always pull the latest image from the registry when a download is requested. To change that, update `berg.challengeImagePullPolicy` to `IfNotPresent`.

For downloading the images, the pull secret defined in `berg.pullSecretName` is used by default. It can be overwritten by setting `downloadImagePullSecret` in the `attachments` entry of the challenge CRD.
