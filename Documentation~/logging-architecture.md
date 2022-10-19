## Logging architecture

The Logging package is structured so that it formats the messages in a deterministic fashion. This allows  an automated system to parse, sort, and filter these logs.

The deterministic nature of these records allow them to be captured and aggregated through dedicated services.

This type of logging solution is well suited for cloud environments where a user is navigating across services. You can identify such a user by a unique identifier, which makes it quicker to track their path across services from the logs and address potential bugs and issues.

## Logging format
Unity Logging follows the [message template](https://messagetemplates.org/) standard and includes built-in static analyzers to validate that the message is correct.

```c#
Log.Info("User {username} logged in from {ip_address}", username, ipAddress);
```

Generates the following in the JSON format:

```json
  {
      "timestamp": "15:43:23 Sep 2 2023",
      "message": "User {username} logged in from {ip_address}",
      "properties":{
       "username": "Alice",
        "ip_address": "123.45.67.89"
      }
    }
```
Generates the following in the text format:
```shell
15:43:23 Sep 2 2023 | Info | User Alice logged in from 123.45.67.89
```

## Asynchronism of the logging solution
To ensure the best performance, the logging solution is asynchronous. Because of this, there is a delay of up to two logging system updates between the capture of the log entry and the display in the console. If the program stops abruptly and the logs haven't been flushed, it might result in a loss of information. 

All Fatal level logs are synchronous to make sure no critical messages are lost. To modify this behavior, use the [SyncMode](xref:Unity.Logging.LogController.SyncMode).
