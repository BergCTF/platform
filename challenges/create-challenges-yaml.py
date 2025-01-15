#!/usr/bin/env python3

import glob, os

os.system("rm -f all-challenges.yaml")

for challenge_yaml_path in glob.glob("**/challenge.yaml"):
    print(f"Adding {challenge_yaml_path}")
    os.system(f"cat {challenge_yaml_path} >> all-challenges.yaml")
    os.system(f"echo >> all-challenges.yaml")
    os.system(f"echo --- >> all-challenges.yaml")