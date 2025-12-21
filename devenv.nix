{
  pkgs,
  lib,
  config,
  inputs,
  ...
}: let
  pkgs-unstable = import inputs.nixpkgs-unstable {system = pkgs.stdenv.system;};
in {
  packages = [pkgs.git];

  languages.opentofu.enable = true;
  languages.opentofu.package = pkgs-unstable.opentofu;

  git-hooks.hooks = {
    terraform-format = {
      enable = true;
      package = pkgs-unstable.opentofu;
    };
  };
}
