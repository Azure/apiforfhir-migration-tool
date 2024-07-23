# Using DemoTruncate.json

Please review the ["De-identified export" section](/FHIR-data-migration-tool-docs#de-identified-export) for more information.



[DemoTruncate.json](/infra/Anonymization/DemoTruncate.json) in this folder is an example of an anonymization configuration file, copied below:

```json
	{
		"fhirVersion": "R4",
		"processingError":"raise",
		"fhirPathRules": [
			{
			"path": "(nodesByType('Observation').value as Quantity).value",
			"method": "generalize",
                   "cases":{
				    "$this.length>18": "$this.toString().substring(0,18).toDecimal()"
			    }
			}
		],
		"parameters": {
			"dateShiftKey": "",
			"cryptoHashKey": "",
			"encryptKey": "",
			"enablePartialAgesForRedact": false
		}
	}
```


- This JSON configuration is for processing FHIR data and contains a rule for handling observation values, particularly for quantities longer than 18 characters.

- *$this.length>18*: This is a condition that checks if the length of the quantity value exceeds 18 characters.
If the condition is true, this transformation is applied. It takes the first 18 characters of the quantity value, converts it to a string, and then converts that substring back to a decimal.
