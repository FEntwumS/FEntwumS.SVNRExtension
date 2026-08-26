library ieee;
use ieee.std_logic_1164.all;

entity mux_4_1 is
    port (
        a, b, c, d : in  std_logic;
        sel        : in  std_logic_vector(1 downto 0);
        y          : out std_logic
    );
end entity mux_4_1;

architecture behavioral of mux_4_1 is
begin
    process (a, b, c, d, sel)
    begin
        case sel is
            when "00" =>
                y <= a;
            when "01" =>
                y <= b;
            when "10" =>
                y <= c;
            when others =>
                y <= d;
        end case;
    end process;
end architecture behavioral;
