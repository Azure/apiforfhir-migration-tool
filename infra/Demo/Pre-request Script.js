const requestBody = request.data;
try {
    const jsonData = JSON.parse(requestBody);
    console.log('Parsed JSON Data:', jsonData);

    jsonData.entry[0].resource.source.name=pm.variables.replaceIn('{{$randomFirstName}}');
    jsonData.entry[0].resource.eventCoding.code=pm.variables.replaceIn('{{$randomInt}}');
    jsonData.entry[2].resource.address[0].country=pm.variables.replaceIn('{{$randomCountry}}');
    jsonData.entry[3].resource.address[0].country=pm.variables.replaceIn('{{$randomCountry}}'); 
    jsonData.entry[88].resource.identifier[0].value=pm.variables.replaceIn('{{$randomInt}}');
    jsonData.entry[87].resource.identifier[0].value=pm.variables.replaceIn('{{$randomAlphaNumeric}}');
    jsonData.entry[86].resource.identifier[0].value=pm.variables.replaceIn('{{$randomAlphaNumeric}}'); 
    jsonData.entry[85].resource.address[0].city=pm.variables.replaceIn('{{$randomCity}}');
    jsonData.entry[85].resource.address[0].country=pm.variables.replaceIn('{{$randomCountry}}');
    jsonData.entry[84].resource.address[0].country=pm.variables.replaceIn('{{$randomCountry}}');
    jsonData.entry[84].resource.name=pm.variables.replaceIn('{{$randomFirstName}}');
    jsonData.entry[84].resource.contact[0].telecom[0].value=pm.variables.replaceIn('{{$randomPhoneNumber}}');
    jsonData.entry[80].resource.identifier[0].value=pm.variables.replaceIn('{{$randomAlphaNumeric}}');
    jsonData.entry[22].resource.identifier[0].value=pm.variables.replaceIn('{{$randomInt}}');
    jsonData.entry[24].resource.identifier[0].value=pm.variables.replaceIn('{{$randomFirstName}}');
    jsonData.entry[26].resource.name[0].prefix[0]=pm.variables.replaceIn('{{$randomNamePrefix}}');
    jsonData.entry[33].resource.name=pm.variables.replaceIn('{{$randomStreetAddress}}');
    jsonData.entry[69].resource.identifier[0].value=pm.variables.replaceIn('{{$randomInt}}');
    jsonData.entry[76].resource.address[0].city=pm.variables.replaceIn('{{$randomCity}}');
    
    const newData = JSON.stringify(jsonData);
    pm.request.body.raw = newData;
   
} catch (error) {
    console.error('Error parsing JSON:', error);
}
