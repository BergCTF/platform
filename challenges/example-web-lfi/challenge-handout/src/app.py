from flask import Flask, request

app = Flask(__name__)

@app.route("/", methods=["GET"])
def index():
    file = request.args.get("file")

    if file:
        with open("files/"+file, "r") as f:
            return f.read()

    return """<!DOCTYPE html>
<html>
    <head>
        <title>Files</title>
    </head>
    <body>
        <h3>Files</h3>
        <a href="/?file=Hello.txt">Hello.txt</a>
        <a href="/?file=World.txt">World.txt</a>
    </body>
</html>
"""

app.run(host="0.0.0.0", port=5000)