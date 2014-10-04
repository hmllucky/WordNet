Installation Instructions

Software Requisites:

1. Proxem Antelope API - http://www.proxem.com/Default.aspx?tabid=55
2. Microsoft Visual Studio 2008 or later.


Step 1: Installing Proxem Antelope API

This library has a version of Wordnet database that is used by the API provided. This implementation uses the database that comes with this library.
 
1. Download Proxem Antelope API from the following link. 
	http://www.proxem.com/Default.aspx?tabid=55

2. Install the software.

Step 2: Set up solution

1. Launch Visual Studio	and open the SentiWordAnalyzer solution.
2. Add/Update reference to Proxem.Antelope libraries under the "References" folder.

Step 3: Configuration

1. The Properties.cs file has paths to three files. This project zip already contains the opFinder_train.txt and opFinder_test.txt files under the data folder.
But the path to "ProxemDataFile" has to be set to the  location of the file "Proxem.Lexicon.dat" . This file will be found under folder where Antelope was installed (Step 1).

Step 4: Running the project

While running the project for the first time, the following steps are recommended.(especially while trying to walk through the code in debug mode).
The RunLeaveOneOutFoldTest() method ( this is the startup method) runs the test for all the 6000 words in file opFinder_train.txt. One could just have a smaller number of words in that list and run the program.

For further questions contact : Guru Devanla - gdevan2@uic.edu






	
	
	


	
	

