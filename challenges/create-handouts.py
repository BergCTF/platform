#!/usr/bin/env python3

import glob, os
from pathlib import Path

os.system("rm -rf handouts")
Path("handouts").mkdir(exist_ok=True)

for handout_path_str in glob.glob("**/challenge-handout", recursive=True):
    handout_path = Path(handout_path_str)
    challenge_name = handout_path.parts[-2]
    if not any(handout_path.iterdir()):
        print("Skipping handout for", challenge_name)
        continue
    os.system(f"cp -L -r {handout_path_str} handouts/{challenge_name}")
    print("Creating handout for", challenge_name)
    os.system(f"tar -czf handouts/{challenge_name}.tar.gz -C handouts {challenge_name}")
    os.system(f"rm -rf handouts/{challenge_name}")