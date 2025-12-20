{
  description = "devshell flake for berg";

  inputs = {
    nixpkgs.url = "github:nixos/nixpkgs?ref=nixos-unstable";
    flake-utils.url = "github:numtide/flake-utils";
  };

  outputs =
    { nixpkgs
    , flake-utils
    , ...
    }: (flake-utils.lib.eachDefaultSystem (system:
    let
      pkgs = nixpkgs.legacyPackages.${system};
    in
    {
      devShells.default = pkgs.mkShell {
        packages = with pkgs; [
          git
          openapi-generator-cli
          mkcert
          kubectl
          kind
          oras
          dotnetCorePackages.sdk_10_0
          csharpier
        ];
      };
    }));
}
