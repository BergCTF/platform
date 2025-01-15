from flask import Flask, request, render_template_string
import os

app = Flask(__name__)

@app.route("/", methods=["GET"])
def index():
    content = request.args.get("content") or ""
    ctx = {
        "os": os
    }
    try:
        return render_template_string("""<!DOCTYPE html>
<html>
    <head>
        <title>Example RCE</title>
    </head>
    <body>
        <p>""" + content + """</p>
        <form action="/" method="GET">
            <input type="text" name="content" value="" />
            <input type="submit" />
        </form>
        <span>Server running with pid {{ os.getpid() }}</span>
    </body>
</html>
""", **ctx)
    except Exception as e:
        return render_template_string("""<!DOCTYPE html>
<html>
    <head>
        <title>Example RCE</title>
    </head>
    <body>
        <h3>Oh no, something went wrong</h3>
        <p>Here are the details:</p>
        <span>{{ ex }}</span>
    </body>
</html>
""", ex=str(e))

app.run(host="0.0.0.0", port=5000)