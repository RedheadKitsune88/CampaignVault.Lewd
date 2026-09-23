# Local NuGet feed

Holds a locally packed `CampaignVault.PluginSdk` until the package is published to nuget.org.

```bash
../scripts/pack-sdk-local.sh
```

`*.nupkg` files here are gitignored. Root `nuget.config` prefers nuget.org, then this folder. After nuget.org publish, remove the `local-packages` source from `nuget.config` and delete this directory's packages.
