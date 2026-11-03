@level1 @level1ui @ui
Feature: Owner reviews group pre-order selections

  Background:
    Given an isolated group-order restaurant is ready
    And the Owner has booked a table through the storefront

  Rule: The Owner sees each guest's current selections

    Scenario: Owner sees submitted, choosing and empty guest carts
      Given invited guests have selected submitted, draft and empty carts through the storefront
      Then the Owner sees every guest's selections, quantities, status and subtotal
      And the consolidated total includes only the confirmed guest cart

    Scenario: Owner sees cart changes without reloading the group
      Given invited guests have selected submitted, draft and empty carts through the storefront
      Then the Owner sees every guest's selections, quantities, status and subtotal
      When the choosing guest adds another portion through the storefront
      Then the Owner automatically sees the changed draft subtotal without adding it to the consolidated total
      When the choosing guest confirms their cart through the storefront
      Then the Owner automatically sees both confirmed carts in the consolidated total

    Scenario: Owner sees an empty group before any guest joins
      Then the Owner sees no guest selections and a zero consolidated total
