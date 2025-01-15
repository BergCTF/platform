FROM python:3
WORKDIR /app
RUN pip install Flask
COPY src/ .
COPY flag.txt /flag.txt
CMD [ "python3", "app.py" ]