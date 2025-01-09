# Challenge

## Dynamic Flags

Its possible to let berg inject dynamic flags into a deployed challenge. This can be done via the new `dynamicFlag` property of the `Challenge` CRD on the `container` spec in one of three ways:

### Environment Variable

```yaml
dynamicFlag:
  env:
    name: FLAG
```

### File Content

```yaml
dynamicFlag:
  content:
    path: /folder/flag.txt
    mode: 0o444 # r--r--r--
```

### Executable Binary

```yaml
dynamicFlag:
  executable:
    path: /folder/runme
    mode: 0o111 # --x--x--x
```

### Full example

```yaml
apiVersion: berg.norelect.ch/v1
kind: Challenge
metadata:
  name: another-nginx
  namespace: berg
spec:
  author: NoRelect
  flag: flag{test_flag}
  description: another-nginx
  difficulty: easy
  categories:
    - web
  tags:
    - nginx
  event: development
  containers:
    - hostname: nginx
      image: nginx:latest
      ports:
        - port: 80
          protocol: tcp
          appProtocol: http
          type: publicHttpRoute
      dynamicFlag:
        content:
          path: /folder/flag.txt
```

When printing the flag, you can see that the dynamic flag modifies the flag specified in the `flag` property to contain random data:

```txt
root@nginx:/# ls -lah /folder/flag.txt
-r--r--r-- 1 root root 29 Jan  8 23:09 /folder/flag.txt
root@nginx:/# cat /folder/flag.txt
flag{test_flag_da3cdd0ac0f9}
root@nginx:/#
```
