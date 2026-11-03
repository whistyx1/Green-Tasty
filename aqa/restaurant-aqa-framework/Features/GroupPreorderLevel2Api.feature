@api @level2 @level2api
Feature: Owner submits a consolidated group pre-order

  Background:
    Given an isolated future group reservation for level two

  Rule: Only confirmed selections become the restaurant order

    Scenario: Confirmed guest and Owner dishes combine while draft dishes stay out
      Given the Owner has a saved dish and guests have confirmed and draft selections
      When the Owner sends the group pre-order
      Then the restaurant order contains the exact combined confirmed quantities
      And the group and order have their submitted statuses

    @negative
    Scenario: The Owner cannot send a group with no confirmed cart
      Given the Owner has a saved dish but no guest has confirmed a cart
      When the Owner sends the group pre-order
      Then submission is refused because no cart was confirmed

    @negative
    Scenario: An empty guest cart cannot become a confirmed cart
      Given an empty guest has joined the level two group
      When that guest tries to confirm the empty cart
      Then the guest is told the cart is empty and the group remains open

    @negative
    Scenario: The Owner cannot send a group without an order
      Given a guest confirmed a dish but the Owner has not created an order
      When the Owner sends the group pre-order
      Then submission is refused because the order is missing

    @negative
    Scenario: The Owner cannot send a group that does not exist
      When the Owner sends a group that does not exist
      Then submission is refused because the group is missing

    @negative
    Scenario: The Owner cannot pair a group with a different reservation
      When the Owner sends the group with a different reservation
      Then submission is refused because the reservation does not match

    @negative
    Scenario: A submitted group cannot be sent a second time
      Given the Owner has a saved dish and guests have confirmed and draft selections
      And the Owner has already sent the group pre-order
      When the Owner sends the group pre-order again
      Then submission is refused because the group is already submitted

  Rule: The reservation Owner alone may submit

    @negative
    Scenario Outline: Another identity cannot send the Owner's group pre-order
      Given the Owner has a saved dish and guests have confirmed and draft selections
      When <requester> sends the group pre-order
      Then <requester> is refused and the group remains open

      Examples: Other identities
        | requester            |
        | another customer     |
        | a guest              |
        | a waiter             |
        | an anonymous visitor |
