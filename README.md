# Additional analyze rules for addressables
### RemoteDependenciesAnalyzeRule
Checks whether local Addressables bundles contain dependencies on remote bundles (references to resources loaded via HTTP). Useful for avoiding issues with resource loading.

![image](https://github.com/avmedvedskiy/AddressablesAnalyzeRules/assets/17832838/59ed07e9-7969-4bda-81eb-c32c365b6d5c)


### TextureCompressionAnalyzeRule
Checks for uncompressed textures and textures with incorrect dimensions (not power-of-two). Helps improve performance and reduce build size.

![image](https://github.com/avmedvedskiy/AddressablesAnalyzeRules/assets/17832838/da636858-2b09-4bb8-8cae-93611f89ce8c)

### AllDependenciesAnalyzeRule
Analyzes cross-dependencies between all Addressable Asset Groups. Used to identify unwanted dependencies between groups.

![image](https://github.com/avmedvedskiy/AddressablesAnalyzeRules/assets/17832838/041758d0-9afe-432c-8f6b-fa52b4133937)



