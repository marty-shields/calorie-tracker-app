Feature: Scan
  The Add sheet's Scan barcode and the search field's barcode button open Scan, which shows what it adds to ("Adding to Dinner · 18 Nov") and asks for the camera. Without it Scan says so. A found product goes straight to Serving: Journeys.feature reaches that through a Debug-build seam (E2EScanSeam) that gives the scanner a barcode. Not found needs a real barcode in front of a camera, so bUnit and the stories cover it. An old /scan link without a meal adds to the meal of the time of day.

  Scenario: The Add sheet opens Scan
    Given I am on the Dashboard
    When I open Scan from the Add sheet
    Then I am on Scan

  Scenario: Scan says which meal and day it adds to and has no bottom bar
    Given I am on the Dashboard
    When I open the Add sheet
    And I choose Dinner on the Add sheet
    And I tap Scan barcode on the Add sheet
    And I refuse the camera permission
    Then Scan says it is adding to Dinner today
    And Scan has no bottom bar

  Scenario: An old link without a meal adds to the meal of the time of day
    Given I am on the Dashboard
    When I open the old link "/scan"
    And I refuse the camera permission
    Then Scan says it is adding to a meal today

  Scenario: Scan offers Search by name instead, which opens search for the same meal
    Given I am on the Dashboard
    When I open the Add sheet
    And I choose Lunch on the Add sheet
    And I tap Scan barcode on the Add sheet
    And I refuse the camera permission
    And I tap Search by name instead on Scan
    Then search is titled "Add to Lunch" with no navigation bar

  Scenario: A refused camera permission shows the camera-unavailable message
    Given I am on the Dashboard
    When I open Scan from the Add sheet
    And I refuse the camera permission
    Then Scan says the camera is unavailable
    And Try again is offered on Scan

  Scenario: Android back leaves Scan
    Given I am on the Dashboard
    And I have opened Scan
    And I have refused the camera permission
    When I press Android back
    Then I am on the Dashboard
