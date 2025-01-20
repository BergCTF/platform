FROM python:3
WORKDIR /app
RUN apt update && apt install -y vim
RUN pip install picows asyncio requests
COPY stress.py .
CMD ["python3", "stress.py"]