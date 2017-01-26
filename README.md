
SParser is a console app, which allows to parse, filter CSV files(RFC 4180) of various encodings and display it in the console window. It supports large files and the UI should be block resistant, thanks to the asynchronization. It certainly requires further testing.

# Usage Sample:
sparser.exe -f "c:\temp\test.csv" -c "filtercol" -v "filterval" 
